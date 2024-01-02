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
    internal class SAMAutoMask
    {
        public  int             points_per_side                = 4;
        private int             points_per_batch               = 64;
        public  float           pred_iou_thresh                = 0.88f;
        public  float           stability_score_thresh         = 0.95f;
        private float           stability_score_offset         = 1.0f;
        private float           box_nms_thresh                 = 0.7f;
        private int             crop_n_layers                  = 0;
        private float           crop_nms_thresh                = 0.7f;
        private float           crop_overlap_ratio             = (float)512 / 1500;
        private int             crop_n_points_downscale_factor = 1;
        private List<double[,]> point_grids                    = null;
        private int             min_mask_region_area           = 0;
        private string          output_mode                    = "binary_mask";
        private Mat             mImage;
        public  SAM             mSAM;
        public  float[]         mImgEmbedding;

        public SAMAutoMask(int points_per_side = 4,
                            int points_per_batch = 64,
                            float pred_iou_thresh = 0.88f,
                            float stability_score_thresh = 0.95f,
                            float stability_score_offset = 1.0f,
                            float box_nms_thresh = 0.7f,
                            int crop_n_layers = 0,
                            float crop_nms_thresh = 0.7f,
                            float crop_overlap_ratio = (float)512 / 1500,
                            int crop_n_points_downscale_factor = 1,
                            List<double[,]> point_grids = null,
                            int min_mask_region_area = 0,
                            string output_mode = "binary_mask")
        {
            this.points_per_side = points_per_side;
            this.points_per_batch = points_per_batch;
            this.pred_iou_thresh = pred_iou_thresh;
            this.stability_score_thresh = stability_score_thresh;
            this.stability_score_offset = stability_score_offset;
            this.box_nms_thresh = box_nms_thresh;
            this.crop_n_layers = crop_n_layers;
            this.crop_nms_thresh = crop_nms_thresh;
            this.crop_overlap_ratio = crop_overlap_ratio;
            this.crop_n_points_downscale_factor = crop_n_points_downscale_factor;
            this.point_grids = point_grids;
            this.min_mask_region_area = min_mask_region_area;
            this.output_mode = output_mode;

            if ((points_per_side == 0) && (point_grids == null || point_grids.Count == 0))
            {
                MessageBox.Show("Exactly one of points_per_side or point_grid must be provided.");
                return;
            }

            if (points_per_side != 0)
            {
                this.point_grids = build_all_layer_point_grids(
                points_per_side,
                crop_n_layers,
                crop_n_points_downscale_factor);
            }
        }
        /// <summary>
        /// 创建网格
        /// </summary>
        /// <param name="n_per_side"></param>
        /// <param name="n_layers"></param>
        /// <param name="scale_per_layer"></param>
        /// <returns></returns>
        private List<double[,]> build_all_layer_point_grids(int n_per_side, int n_layers, int scale_per_layer)
        {
            var points_by_layer = new List<double[,]>();
            for (var i = 0; i <= n_layers; i++)
            {
                var n_points = (int)(n_per_side / Math.Pow(scale_per_layer, i));
                points_by_layer.Add(BuildPointGrid(n_points));
            }
            return points_by_layer;
        }

        private double[,] BuildPointGrid(int n_per_side)
        {
            var offset = 1.0 / (2 * n_per_side);
            var points_one_side = Enumerable.Range(0, n_per_side)
                .Select(i => offset + i * (1.0 - 2 * offset) / (n_per_side - 1))
                .ToArray();

            var points = new double[n_per_side * n_per_side, 2];

            for (var i = 0; i < n_per_side; i++)
            {
                for (var j = 0; j < n_per_side; j++)
                {
                    var index = i * n_per_side + j;
                    points[index, 0] = points_one_side[i];
                    points[index, 1] = points_one_side[j];
                }
            }

            return points;
        }

     
        /// <summary>
        ///  Generates a list of crop boxes of different sizes. Each layer
        ///  has(2**i)**2 boxes for the ith layer.
        /// </summary>
        private void generateCropBoxes(int orgWid, int orgHei, int n_layers, float overlap_ratio,
            ref List<List<int>> crop_boxes, ref List<int> layer_idxs)
        {
            var im_h = orgHei;
            var im_w = orgWid;

            var short_side = Math.Min(im_h, im_w);
            //Original image
            crop_boxes.Add(new List<int> { 0, 0, im_w, im_h });
            layer_idxs.Add(0);

            for (var i_layer = 0; i_layer < n_layers; i_layer++)
            {
                var n_crops_per_side = (int)Math.Pow(2, i_layer + 1);
                var overlap = (int)(overlap_ratio * short_side * (2 / n_crops_per_side));

                var crop_w = crop_len(im_w, n_crops_per_side, overlap);
                var crop_h = crop_len(im_h, n_crops_per_side, overlap);

                var crop_box_x0 = new List<int>();
                var crop_box_y0 = new List<int>();

                for (var i = 0; i < n_crops_per_side; i++)
                {
                    crop_box_x0.Add((crop_w - overlap) * i);
                    crop_box_y0.Add((crop_h - overlap) * i);
                }

                foreach (var x0 in crop_box_x0)
                {
                    foreach (var y0 in crop_box_y0)
                    {
                        var box = new List<int>
                        {
                            x0,
                            y0,
                            Math.Min(x0 + crop_w, im_w),
                            Math.Min(y0 + crop_h, im_h)
                        };

                        crop_boxes.Add(box);
                        layer_idxs.Add(i_layer + 1);
                    }
                }

            }
        }

        private int crop_len(int orig_len, int n_crops, int overlap)
        {
            return (int)(Math.Ceiling((double)(overlap * (n_crops - 1) + orig_len) / n_crops));
        }

        private IEnumerable<List<object>> BatchIterator(int batch_size, params object[] args)
        {

            var n_batches = ((Array)args[0]).Length / batch_size + (((Array)args[0]).Length % batch_size != 0 ? 1 : 0);
            for (var b = 0; b < n_batches; b++)
            {
                var batch = new List<object>();
                foreach (var arg in args)
                {
                    var arr = (Array)arg;
                    var start = b * batch_size;
                    var end = (b + 1) * batch_size;
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
            mImage?.Dispose();

            mImage = Cv2.ImRead(imgfile);
            if (points_per_side != 0)
            {
                point_grids = build_all_layer_point_grids(
                points_per_side,
                crop_n_layers,
                crop_n_points_downscale_factor);
            }
            var masks = _generate_masks(mImage);
            return masks;
        }

        private MaskData _generate_masks(Mat img)
        {
            var masks = new MaskData();
            var crop_boxes = new List<List<int>>();
            var layer_idxs = new List<int>();

            generateCropBoxes(img.Cols, img.Rows, crop_n_layers, crop_overlap_ratio, ref crop_boxes, ref layer_idxs);

            for (var i = 0; i < crop_boxes.Count; i++)
            {
                var mask = _process_crop(crop_boxes[i], layer_idxs[i]);
                masks.Cat(mask);
            }

            return masks;
        }

        private MaskData _process_crop(List<int> crop_box, int crop_layer_idx)
        {
            var masks = new MaskData();
            int x0 = crop_box[0], y0 = crop_box[1], x1 = crop_box[2], y1 = crop_box[3];
            // 定义ROI的矩形区域
            var roiRect = new OpenCvSharp.Rect(x0, y0, x1, y1); // (x, y, width, height)
            // 提取ROI区域
            var roiImage = new Mat(mImage, roiRect);
            //图像编码
            //this.mImgEmbedding = this.mSAM.Encode(roiImage, roiImage.Cols, roiImage.Rows);

            var gps = point_grids[crop_layer_idx];
            var points_for_image = new PointPromotion[gps.GetLength(0)];
            var ts = new Transforms(1024);
            for (var i = 0; i < gps.GetLength(0); i++)
            {
                Promotion promt = new PointPromotion(OpType.ADD);
                (promt as PointPromotion).X = (int)(gps[i, 0] * roiImage.Cols);
                (promt as PointPromotion).Y = (int)(gps[i, 1] * roiImage.Rows);
                var ptn = ts.ApplyCoords((promt as PointPromotion), roiImage.Cols, roiImage.Rows);
                points_for_image[i] = ptn;
            }
            object[] args = { points_for_image };
            foreach (var v in BatchIterator(points_per_batch, args))
            {
                var mask = _process_batch(v.ToList(), roiImage.Cols, roiImage.Rows, crop_box, mImage.Cols, mImage.Rows);
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
                    var md = mSAM.Decode(new List<Promotion>() { pts[i] }, mImgEmbedding, cropimgwid, cropimghei);
                    md.mStalibility = md.CalculateStabilityScore(mSAM.mask_threshold, stability_score_offset).ToList();
                    md.Filter(pred_iou_thresh, stability_score_thresh);
                    batch.Cat(md);
                }
                          
            }

            batch.mBox = batch.batched_mask_to_box().ToList();
            return batch;
        }

    }
}
