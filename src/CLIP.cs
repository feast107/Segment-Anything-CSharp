using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace SAMViewer
{
    /// <summary>
    /// Image And Text Encoder
    /// </summary>
    internal class Clip
    {
        private       InferenceSession? txtEncoder;
        private       InferenceSession? imgEncoder;
        public static Clip?             TheSingleton;
        private const int               ContextLength = 77;

        protected Clip()
        {
            var thread = new Thread(LoadOnnxModel);
            thread.Start();       
        }
        public static Clip Instance()
        {
            return TheSingleton ??= new Clip();
        }

        /// <summary>
        /// 加载CLIP模型
        /// </summary>
        private void LoadOnnxModel()
        {
            txtEncoder?.Dispose();

            imgEncoder?.Dispose();

            var exePath     = AppContext.BaseDirectory;
            var textEncoder = Path.Combine(exePath, "textual.onnx");
            if (!File.Exists(textEncoder))
            {
                MessageBox.Show(textEncoder + " not exist!");
                return;
            }
            txtEncoder = new InferenceSession(textEncoder);

            var imgencoder = exePath + @"\visual.onnx";
            if (!File.Exists(imgencoder))
            {
                MessageBox.Show(imgencoder + " not exist!");
                return;
            }
            imgEncoder = new InferenceSession(imgencoder);
        }


        /// <summary>
        /// CLIP对文本进行编码
        /// </summary>
        public List<float> TxtEncoder(string txt)
        {
            var token = SimpleTokenizer.Instance().Tolikenlize(txt);
            var txtTensor = new DenseTensor<Int64>(token.ToArray(), new[] { 1,ContextLength });
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", txtTensor)
            };

            var results = txtEncoder.Run(inputs);
            var result = results.First().AsTensor<float>().ToArray();

            return result.ToList();
        }
        /// <summary>
        /// CLIP对图像进行编码
        /// </summary>
        public IEnumerable<float> ImgEncoder(float[] img)
        {
            var tensor = new DenseTensor<float>(img, new[] { 1, 3, 224, 224 });
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", tensor)
            };

            var results = imgEncoder.Run(inputs);
            var result = results.First().AsTensor<float>().ToArray();

            return result.ToList();
        }
    }
}
