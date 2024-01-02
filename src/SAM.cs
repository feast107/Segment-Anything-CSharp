using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System.IO;
using System.Windows;

namespace SAMViewer
{
    /// <summary>
    /// Segment Anything
    /// </summary>
    internal class Sam
    {
        public static Sam              TheSingleton = null;
        private       InferenceSession encoder;
        private       InferenceSession decoder;
        public        float            MaskThreshold = 0.0f;
        private       bool             ready         = false;
        protected Sam()
        {
           
        }
        public static Sam Instance()
        {
            if (null == TheSingleton)
            {
                TheSingleton = new Sam();
            }
            return TheSingleton;
        }
        /// <summary>
        /// 加载Segment Anything模型
        /// </summary>
        public void LoadOnnxModel()
        {
            if (encoder != null)
                encoder.Dispose();

            if (decoder != null)
                decoder.Dispose();

            var options = new SessionOptions();
            options.EnableMemoryPattern = false;
            options.EnableCpuMemArena = false;

            var exePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var encodeModelPath = exePath + @"\encoder-quant.onnx";
            if (!File.Exists(encodeModelPath))
            {
                MessageBox.Show(encodeModelPath + " not exist!");
                return;
            }
            encoder = new InferenceSession(encodeModelPath, options);

            var decodeModelPath = exePath + @"\decoder-quant.onnx";
            if (!File.Exists(decodeModelPath))
            {
                MessageBox.Show(decodeModelPath + " not exist!");
                return;
            }
            decoder = new InferenceSession(decodeModelPath, options);
        }
        /// <summary>
        /// Segment Anything对图像进行编码
        /// </summary>
        public float[] Encode(Mat image,int orgWid,int orgHei)
        {
            var tranform = new Transforms(1024);

            var img = tranform.ApplyImage(image, orgWid, orgHei);          
            var tensor = new DenseTensor<float>(img, new[] { 1, 3, 1024, 1024 });
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("x", tensor)
            };

            var results = encoder.Run(inputs);
            var embedding = results.First().AsTensor<float>().ToArray();
            ready = true;

            return embedding;
        }

        /// <summary>
        /// Segment Anything提示信息解码
        /// </summary>
        public MaskData Decode(List<Promotion> promotions, float[] embedding, int orgWid, int orgHei)
        {
            if (ready == false)
            {
                MessageBox.Show("Image Embedding is not done!");
                return null;
            }

            var embeddingTensor = new DenseTensor<float>(embedding, new[] { 1, 256, 64, 64 });

            var bpmos = promotions.FindAll(e => e.Type == PromotionType.Box);
            var pproms = promotions.FindAll(e => e.Type == PromotionType.Point);
            var boxCount = promotions.FindAll(e => e.Type == PromotionType.Box).Count();
            var pointCount = promotions.FindAll(e => e.Type == PromotionType.Point).Count();
            var promotion = new float[2 * (boxCount * 2 + pointCount)];
            var label = new float[boxCount * 2 + pointCount];
            for (var i = 0; i < boxCount; i++)
            {
                var input = bpmos[i].GetInput();
                for (var j = 0; j < input.Count(); j++)
                {
                    promotion[4 * i + j] = input[j];
                }
                var la = bpmos[i].GetLable();
                for (var j = 0; j < la.Count(); j++)
                {
                    label[2 * i + j] = la[j];
                }
            }
            for (var i = 0; i < pointCount; i++)
            {
                var p = pproms[i].GetInput();
                for (var j = 0; j < p.Count(); j++)
                {
                    promotion[boxCount * 4 + 2 * i + j] = p[j];
                }
                var la = pproms[i].GetLable();
                for (var j = 0; j < la.Count(); j++)
                {
                    label[boxCount * 2 + i + j] = la[j];
                }
            }

            var pointCoordsTensor = new DenseTensor<float>(promotion, new[] { 1, boxCount * 2 + pointCount, 2 });

            var pointLabelTensor = new DenseTensor<float>(label, new[] { 1, boxCount * 2 + pointCount });

            var mask = new float[256 * 256];
            for (var i = 0; i < mask.Count(); i++)
            {
                mask[i] = 0;
            }
            var maskTensor = new DenseTensor<float>(mask, new[] { 1, 1, 256, 256 });

            var hasMaskValues = new float[1] { 0 };
            var hasMaskValuesTensor = new DenseTensor<float>(hasMaskValues, new[] { 1 });

            float[] origImSizeValues = { orgHei, orgWid };
            var origImSizeValuesTensor = new DenseTensor<float>(origImSizeValues, new[] { 2 });

            var decodeInputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("image_embeddings", embeddingTensor),
                NamedOnnxValue.CreateFromTensor("point_coords", pointCoordsTensor),
                NamedOnnxValue.CreateFromTensor("point_labels", pointLabelTensor),
                NamedOnnxValue.CreateFromTensor("mask_input", maskTensor),
                NamedOnnxValue.CreateFromTensor("has_mask_input", hasMaskValuesTensor),
                NamedOnnxValue.CreateFromTensor("orig_im_size", origImSizeValuesTensor)
            };
            var md = new MaskData();
            var segmask = decoder.Run(decodeInputs).ToList();
            md.Mask = segmask[0].AsTensor<float>().ToArray().ToList();
            md.Shape = segmask[0].AsTensor<float>().Dimensions.ToArray();
            md.IoU = segmask[1].AsTensor<float>().ToList();
            return md;

        }

    }

}
    

