using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace SAMViewer
{
    /// <summary>
    /// 自动分割Everything
    /// </summary>
    internal class SamAutoMask
    {
        public           int             PointsPerSide                = 4;
        private readonly int             pointsPerBatch               = 64;
        public           float           PredIouThresh                = 0.88f;
        public           float           StabilityScoreThresh         = 0.95f;
        private readonly float           stabilityScoreOffset         = 1.0f;
        private          float           boxNmsThresh                 = 0.7f;
        private readonly int             cropNLayers                  = 0;
        private          float           cropNmsThresh                = 0.7f;
        private readonly float           cropOverlapRatio             = (float)512 / 1500;
        private readonly int             cropNPointsDownscaleFactor = 1;
        private          List<double[,]> pointGrids                    = null;
        private          int             minMaskRegionArea           = 0;
        private          string          outputMode                    = "binary_mask";
        private          Mat             image;
        public           Sam             Sam;
        public           float[]         ImgEmbedding;

        public SamAutoMask(int pointsPerSide = 4,
                            int pointsPerBatch = 64,
                            float predIouThresh = 0.88f,
                            float stabilityScoreThresh = 0.95f,
                            float stabilityScoreOffset = 1.0f,
                            float boxNmsThresh = 0.7f,
                            int cropNLayers = 0,
                            float cropNmsThresh = 0.7f,
                            float cropOverlapRatio = (float)512 / 1500,
                            int cropNPointsDownscaleFactor = 1,
                            List<double[,]> pointGrids = null,
                            int minMaskRegionArea = 0,
                            string outputMode = "binary_mask")
        {
            this.PointsPerSide = pointsPerSide;
            this.pointsPerBatch = pointsPerBatch;
            this.PredIouThresh = predIouThresh;
            this.StabilityScoreThresh = stabilityScoreThresh;
            this.stabilityScoreOffset = stabilityScoreOffset;
            this.boxNmsThresh = boxNmsThresh;
            this.cropNLayers = cropNLayers;
            this.cropNmsThresh = cropNmsThresh;
            this.cropOverlapRatio = cropOverlapRatio;
            this.cropNPointsDownscaleFactor = cropNPointsDownscaleFactor;
            this.pointGrids = pointGrids;
            this.minMaskRegionArea = minMaskRegionArea;
            this.outputMode = outputMode;

            if ((pointsPerSide == 0) && (pointGrids == null || pointGrids.Count == 0))
            {
                MessageBox.Show("Exactly one of points_per_side or point_grid must be provided.");
                return;
            }

            if (pointsPerSide != 0)
            {
                this.pointGrids = build_all_layer_point_grids(
                pointsPerSide,
                cropNLayers,
                cropNPointsDownscaleFactor);
            }
        }
        /// <summary>
        /// 创建网格
        /// </summary>
        /// <param name="n_per_side"></param>
        /// <param name="n_layers"></param>
        /// <param name="scale_per_layer"></param>
        /// <returns></returns>
        private List<double[,]> build_all_layer_point_grids(int nPerSide, int nLayers, int scalePerLayer)
        {
            var pointsByLayer = new List<double[,]>();
            for (var i = 0; i <= nLayers; i++)
            {
                var nPoints = (int)(nPerSide / Math.Pow(scalePerLayer, i));
                pointsByLayer.Add(BuildPointGrid(nPoints));
            }
            return pointsByLayer;
        }

        private double[,] BuildPointGrid(int nPerSide)
        {
            var offset = 1.0 / (2 * nPerSide);
            var pointsOneSide = Enumerable.Range(0, nPerSide)
                .Select(i => offset + i * (1.0 - 2 * offset) / (nPerSide - 1))
                .ToArray();

            var points = new double[nPerSide * nPerSide, 2];

            for (var i = 0; i < nPerSide; i++)
            {
                for (var j = 0; j < nPerSide; j++)
                {
                    var index = i * nPerSide + j;
                    points[index, 0] = pointsOneSide[i];
                    points[index, 1] = pointsOneSide[j];
                }
            }

            return points;
        }

     
        /// <summary>
        ///  Generates a list of crop boxes of different sizes. Each layer
        ///  has(2**i)**2 boxes for the ith layer.
        /// </summary>
        private void GenerateCropBoxes(int orgWid, int orgHei, int nLayers, float overlapRatio,
            ref List<List<int>> cropBoxes, ref List<int> layerIdxs)
        {
            var imH = orgHei;
            var imW = orgWid;

            var shortSide = Math.Min(imH, imW);
            //Original image
            cropBoxes.Add(new List<int> { 0, 0, imW, imH });
            layerIdxs.Add(0);

            for (var iLayer = 0; iLayer < nLayers; iLayer++)
            {
                var nCropsPerSide = (int)Math.Pow(2, iLayer + 1);
                var overlap = (int)(overlapRatio * shortSide * (2 / nCropsPerSide));

                var cropW = crop_len(imW, nCropsPerSide, overlap);
                var cropH = crop_len(imH, nCropsPerSide, overlap);

                var cropBoxX0 = new List<int>();
                var cropBoxY0 = new List<int>();

                for (var i = 0; i < nCropsPerSide; i++)
                {
                    cropBoxX0.Add((cropW - overlap) * i);
                    cropBoxY0.Add((cropH - overlap) * i);
                }

                foreach (var x0 in cropBoxX0)
                {
                    foreach (var y0 in cropBoxY0)
                    {
                        var box = new List<int>
                        {
                            x0,
                            y0,
                            Math.Min(x0 + cropW, imW),
                            Math.Min(y0 + cropH, imH)
                        };

                        cropBoxes.Add(box);
                        layerIdxs.Add(iLayer + 1);
                    }
                }

            }
        }

        private int crop_len(int origLen, int nCrops, int overlap)
        {
            return (int)(Math.Ceiling((double)(overlap * (nCrops - 1) + origLen) / nCrops));
        }

        private IEnumerable<List<object>> BatchIterator(int batchSize, params object[] args)
        {

            var nBatches = ((Array)args[0]).Length / batchSize + (((Array)args[0]).Length % batchSize != 0 ? 1 : 0);
            for (var b = 0; b < nBatches; b++)
            {
                var batch = new List<object>();
                foreach (var arg in args)
                {
                    var arr = (Array)arg;
                    var start = b * batchSize;
                    var end = (b + 1) * batchSize;
                    if (end > arr.Length)
                        end = arr.Length;

                    var slice = Array.CreateInstance(arr.GetType().GetElementType(), end - start);
                    Array.Copy(arr, start, slice, 0, end - start); 
                    batch.Add(slice);
                }

                yield return batch;
            }
        }
        public MaskData Generate(string imgfile)
        {
            image?.Dispose();

            image = Cv2.ImRead(imgfile);
            if (PointsPerSide != 0)
            {
                pointGrids = build_all_layer_point_grids(
                PointsPerSide,
                cropNLayers,
                cropNPointsDownscaleFactor);
            }
            var masks = _generate_masks(image);
            return masks;
        }

        private MaskData _generate_masks(Mat img)
        {
            var masks = new MaskData();
            var cropBoxes = new List<List<int>>();
            var layerIdxs = new List<int>();

            GenerateCropBoxes(img.Cols, img.Rows, cropNLayers, cropOverlapRatio, ref cropBoxes, ref layerIdxs);

            for (var i = 0; i < cropBoxes.Count; i++)
            {
                var mask = _process_crop(cropBoxes[i], layerIdxs[i]);
                masks.Cat(mask);
            }

            return masks;
        }

        private MaskData _process_crop(List<int> cropBox, int cropLayerIdx)
        {
            var masks = new MaskData();
            int x0 = cropBox[0], y0 = cropBox[1], x1 = cropBox[2], y1 = cropBox[3];
            // 定义ROI的矩形区域
            var roiRect = new OpenCvSharp.Rect(x0, y0, x1, y1); // (x, y, width, height)
            // 提取ROI区域
            var roiImage = new Mat(image, roiRect);
            //图像编码
            //this.mImgEmbedding = this.mSAM.Encode(roiImage, roiImage.Cols, roiImage.Rows);

            var gps = pointGrids[cropLayerIdx];
            var pointsForImage = new PointPromotion[gps.GetLength(0)];
            var ts = new Transforms(1024);
            for (var i = 0; i < gps.GetLength(0); i++)
            {
                Promotion promt = new PointPromotion(OpType.Add);
                (promt as PointPromotion).X = (int)(gps[i, 0] * roiImage.Cols);
                (promt as PointPromotion).Y = (int)(gps[i, 1] * roiImage.Rows);
                var ptn = ts.ApplyCoords((promt as PointPromotion), roiImage.Cols, roiImage.Rows);
                pointsForImage[i] = ptn;
            }
            object[] args = { pointsForImage };
            foreach (var v in BatchIterator(pointsPerBatch, args))
            {
                var mask = _process_batch(v.ToList(), roiImage.Cols, roiImage.Rows, cropBox, image.Cols, image.Rows);
                masks.Cat(mask);
            }
            roiImage.Dispose();
            return masks;
        }

        private MaskData _process_batch(List<object> points,int cropimgwid,int cropimghei,List<int>cropbox,
            int orgimgwid, int orgimghei)
        {
            var batch = new MaskData();
            var masks = new List<float[]>();
            foreach (var v in points)
            {
                var pts = v as PointPromotion[];
                for (var i=0;i<pts.Length;i++)
                {
                    var md = Sam.Decode(new List<Promotion>() { pts[i] }, ImgEmbedding, cropimgwid, cropimghei);
                    md.Stalibility = md.CalculateStabilityScore(Sam.MaskThreshold, stabilityScoreOffset).ToList();
                    md.Filter(PredIouThresh, StabilityScoreThresh);
                    batch.Cat(md);
                }
                          
            }

            batch.Box = batch.batched_mask_to_box().ToList();
            return batch;
        }

    }
}
