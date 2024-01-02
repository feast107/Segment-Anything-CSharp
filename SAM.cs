﻿using Microsoft.ML.OnnxRuntime;
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
    /// Segment Anything
    /// </summary>
    internal class SAM
    {
        public static SAM              theSingleton = null;
        private       InferenceSession mEncoder;
        private       InferenceSession mDecoder;
        public        float            mask_threshold = 0.0f;
        private       bool             mReady         = false;
        protected SAM()
        {
           
        }
        public static SAM Instance()
        {
            if (null == theSingleton)
            {
                theSingleton = new SAM();
            }
            return theSingleton;
        }
        /// <summary>
        /// 加载Segment Anything模型
        /// </summary>
        public void LoadONNXModel()
        {
            if (mEncoder != null)
                mEncoder.Dispose();

            if (mDecoder != null)
                mDecoder.Dispose();

            var options = new SessionOptions();
            options.EnableMemoryPattern = false;
            options.EnableCpuMemArena = false;

            var exePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var encode_model_path = exePath + @"\encoder-quant.onnx";
            if (!File.Exists(encode_model_path))
            {
                MessageBox.Show(encode_model_path + " not exist!");
                return;
            }
            mEncoder = new InferenceSession(encode_model_path, options);

            var decode_model_path = exePath + @"\decoder-quant.onnx";
            if (!File.Exists(decode_model_path))
            {
                MessageBox.Show(decode_model_path + " not exist!");
                return;
            }
            mDecoder = new InferenceSession(decode_model_path, options);
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

            var results = mEncoder.Run(inputs);
            var embedding = results.First().AsTensor<float>().ToArray();
            mReady = true;

            return embedding;
        }

        /// <summary>
        /// Segment Anything提示信息解码
        /// </summary>
        public MaskData Decode(List<Promotion> promotions, float[] embedding, int orgWid, int orgHei)
        {
            if (mReady == false)
            {
                MessageBox.Show("Image Embedding is not done!");
                return null;
            }

            var embedding_tensor = new DenseTensor<float>(embedding, new[] { 1, 256, 64, 64 });

            var bpmos = promotions.FindAll(e => e.mType == PromotionType.Box);
            var pproms = promotions.FindAll(e => e.mType == PromotionType.Point);
            var boxCount = promotions.FindAll(e => e.mType == PromotionType.Box).Count();
            var pointCount = promotions.FindAll(e => e.mType == PromotionType.Point).Count();
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

            var point_coords_tensor = new DenseTensor<float>(promotion, new[] { 1, boxCount * 2 + pointCount, 2 });

            var point_label_tensor = new DenseTensor<float>(label, new[] { 1, boxCount * 2 + pointCount });

            var mask = new float[256 * 256];
            for (var i = 0; i < mask.Count(); i++)
            {
                mask[i] = 0;
            }
            var mask_tensor = new DenseTensor<float>(mask, new[] { 1, 1, 256, 256 });

            var hasMaskValues = new float[1] { 0 };
            var hasMaskValues_tensor = new DenseTensor<float>(hasMaskValues, new[] { 1 });

            float[] orig_im_size_values = { orgHei, orgWid };
            var orig_im_size_values_tensor = new DenseTensor<float>(orig_im_size_values, new[] { 2 });

            var decode_inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("image_embeddings", embedding_tensor),
                NamedOnnxValue.CreateFromTensor("point_coords", point_coords_tensor),
                NamedOnnxValue.CreateFromTensor("point_labels", point_label_tensor),
                NamedOnnxValue.CreateFromTensor("mask_input", mask_tensor),
                NamedOnnxValue.CreateFromTensor("has_mask_input", hasMaskValues_tensor),
                NamedOnnxValue.CreateFromTensor("orig_im_size", orig_im_size_values_tensor)
            };
            var md = new MaskData();
            var segmask = mDecoder.Run(decode_inputs).ToList();
            md.mMask = segmask[0].AsTensor<float>().ToArray().ToList();
            md.mShape = segmask[0].AsTensor<float>().Dimensions.ToArray();
            md.mIoU = segmask[1].AsTensor<float>().ToList();
            return md;

        }

    }

}
    

