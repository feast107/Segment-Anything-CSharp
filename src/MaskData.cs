namespace SAMViewer
{
    /// <summary>
    /// A structure for storing masks and their related data in batched format.
    /// Implements basic filtering and concatenation.
    /// </summary>
    internal class MaskData
    {
        public int[]       Shape;
        public List<float> Mask;
        public List<float> IoU;
        public List<float> Stalibility;
        public List<int>   Box;

        public List<List<float>> MfinalMask;

        public MaskData()
        {
            Shape       = new int[4];
            Mask        = new List<float>();
            IoU         = new List<float>();
            Stalibility = new List<float>();
            Box         = new List<int>();

            MfinalMask = new List<List<float>>();
        }

        public void Filter(float predIouThresh, float stabilityScoreThresh)
        {
            var m     = new List<float>();
            var i     = new List<float>();
            var s     = new List<float>();
            var batch = 0;
            for (var j = 0; j < Shape[1]; j++)
            {
                if (IoU[j] > predIouThresh && Stalibility[j] > stabilityScoreThresh)
                {
                    MfinalMask.Add(Mask.GetRange(j * Shape[2] * Shape[3], Shape[2] * Shape[3]));
                    //m.AddRange(this.mMask.GetRange(j * this.mShape[2] * this.mShape[3], this.mShape[2] * this.mShape[3]));
                    i.Add(IoU[j]);
                    s.Add(Stalibility[j]);
                    batch++;
                }
            }

            Shape[1] = batch;
            Stalibility.Clear();
            Mask.Clear();
            IoU.Clear();
            //this.mMask.AddRange(m);
            IoU.AddRange(i);
            Stalibility.AddRange(s);
        }


        public float[] CalculateStabilityScore(float maskThreshold, float thresholdOffset)
        {
            var batchSize = Shape[1];
            var width     = Shape[3];
            var height    = Shape[2];

            var intersections = new float[batchSize];
            var unions        = new float[batchSize];

            for (var i = 0; i < batchSize; i++)
            {
                float intersectionSum = 0;
                float unionSum        = 0;

                for (var j = 0; j < width; j++)
                {
                    for (var k = 0; k < height; k++)
                    {
                        var index = i * width * height + k * width + j;
                        if (Mask[index] > maskThreshold + thresholdOffset)
                        {
                            intersectionSum++;
                        }

                        if (Mask[index] > maskThreshold - thresholdOffset)
                        {
                            unionSum++;
                        }
                    }
                }

                intersections[i] = intersectionSum;
                unions[i]        = unionSum;
            }

            var stabilityScores = new float[batchSize];
            for (var i = 0; i < batchSize; i++)
            {
                stabilityScores[i] = intersections[i] / unions[i];
            }

            return stabilityScores;
        }

        public void Cat(MaskData md)
        {
            Shape[0] =  md.Shape[0];
            Shape[1] += md.Shape[1];
            Shape[2] =  md.Shape[2];
            Shape[3] =  md.Shape[3];
            Box.AddRange(md.Box);
            Mask.AddRange(md.Mask);
            Stalibility.AddRange(md.Stalibility);
            IoU.AddRange(md.IoU);

            MfinalMask.AddRange(md.MfinalMask);
        }

        public IEnumerable<int> batched_mask_to_box()
        {
            var C      = Shape[1];
            var width  = Shape[3];
            var height = Shape[2];

            var boxes = new int[C * 4];

            for (var c = 0; c < C; c++)
            {
                var emptyMask = true;
                var top       = height;
                var bottom    = 0;
                var left      = width;
                var right     = 0;

                for (var i = 0; i < width; i++)
                {
                    for (var j = 0; j < height; j++)
                    {
                        //int index = c * width * height + j * width + i;
                        //if (this.mMask[index] > 0)
                        var index = j * width + i;
                        if (MfinalMask[c][index] > 0)
                        {
                            emptyMask = false;
                            top       = Math.Min(top, j);
                            bottom    = Math.Max(bottom, j);
                            left      = Math.Min(left, i);
                            right     = Math.Max(right, i);
                        }
                    }
                }

                if (emptyMask)
                {
                    boxes[c * 4]     = 0;
                    boxes[c * 4 + 1] = 0;
                    boxes[c * 4 + 2] = 0;
                    boxes[c * 4 + 3] = 0;
                }
                else
                {
                    boxes[c * 4]     = left;
                    boxes[c * 4 + 1] = top;
                    boxes[c * 4 + 2] = right;
                    boxes[c * 4 + 3] = bottom;
                }
            }

            return boxes;
        }
    }
}