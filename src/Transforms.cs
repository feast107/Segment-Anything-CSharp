using OpenCvSharp;

namespace SAMViewer
{
    /// <summary>
    ///  Resizes images to the longest side 'target_length', as well as provides
    ///  methods for resizing coordinates and boxes. Provides methods for
    ///  transforming both numpy array and batched torch tensors.
    /// </summary>
    internal class Transforms
    {
        public Transforms(int targetLength)
        {
            this.targetLength = targetLength;
        }
        /// <summary>
        /// 变换图像，将原始图像变换大小
        /// </summary>
        /// <returns></returns>
        public float[] ApplyImage(Mat image, int orgw, int orgh)
        {
            var neww = 0;
            var newh = 0;
            GetPreprocessShape(orgw, orgh, targetLength, ref neww, ref newh);

            // 缩放图像
            var resizedImage = new Mat();
            Cv2.Resize(image, resizedImage, new OpenCvSharp.Size(neww, newh));
            //将图像转换为浮点型
            var floatImage = new Mat();
            resizedImage.ConvertTo(floatImage, MatType.CV_32FC3);

            // 计算均值和标准差
            Scalar mean, stddev;
            Cv2.MeanStdDev(floatImage, out mean, out stddev);

            // 标准化图像
            var normalizedImage = new Mat();
            Cv2.Subtract(floatImage, mean, normalizedImage);
            Cv2.Divide(normalizedImage, stddev, normalizedImage);

            var transformedImg = new float[3 * targetLength * targetLength];
            for (var i = 0; i < neww; i++)
            {
                for (var j = 0; j < newh; j++)
                {
                    var index = j * targetLength + i;
                    transformedImg[index] = normalizedImage.At<Vec3f>(j, i)[0];
                    transformedImg[targetLength * targetLength + index] = normalizedImage.At<Vec3f>(j, i)[1];
                    transformedImg[2 * targetLength * targetLength + index] = normalizedImage.At<Vec3f>(j, i)[2];
                }
            }
            resizedImage.Dispose();
            floatImage.Dispose();
            normalizedImage.Dispose();

            return transformedImg;
        }
      
        public PointPromotion ApplyCoords(PointPromotion orgPoint, int orgw, int orgh)
        {
            var neww = 0;
            var newh = 0;
            GetPreprocessShape(orgw, orgh, targetLength, ref neww, ref newh);
            var newpointp = new PointPromotion(orgPoint.Optype);
            var scalx = 1.0f * neww / orgw;
            var scaly = 1.0f * newh / orgh;
            newpointp.X = (int)(orgPoint.X * scalx);
            newpointp.Y = (int)(orgPoint.Y * scaly);

            return newpointp;
        }
        public BoxPromotion ApplyBox(BoxPromotion orgBox, int orgw, int orgh)
        {
            var box = new BoxPromotion();

            var left = ApplyCoords(orgBox.mLeftUp, orgw, orgh);
            var lefrightt = ApplyCoords(orgBox.mRightBottom, orgw, orgh);

            box.mLeftUp = left;
            box.mRightBottom = lefrightt;
            return box;
        }

        public void GetPreprocessShape(int oldw, int oldh, int longSideLength, ref int neww, ref int newh)
        {
            var scale = longSideLength * 1.0f / Math.Max(oldh, oldw);
            var newht = oldh * scale;
            var newwt = oldw * scale;

            neww = (int)(newwt + 0.5);
            newh = (int)(newht + 0.5);
        }

        private readonly int targetLength; //目标图像大小（宽=高）


    }
}