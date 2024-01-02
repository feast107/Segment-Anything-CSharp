using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAMViewer
{
    /// <summary>
    /// A structure for storing masks and their related data in batched format.
    /// Implements basic filtering and concatenation.
    /// </summary>
    internal class MaskData
    {
        public int[] mShape;
        public List<float> mMask;
        public List<float> mIoU;
        public List<float> mStalibility;
        public List<int> mBox;

        public List<List<float>> mfinalMask;
        public MaskData()
        {
            mShape = new int[4];
            mMask = new List<float>();
            mIoU = new List<float>();
            mStalibility = new List<float>();
            mBox = new List<int>();

            mfinalMask = new List<List<float>>();
        }

        public void Filter(float pred_iou_thresh, float stability_score_thresh)
        {
            var m = new List<float>();
            var i = new List<float>();
            var s = new List<float>();
            var batch = 0;
            for (var j = 0; j < mShape[1]; j++)
            {
                if (mIoU[j] >  pred_iou_thresh && mStalibility[j]> stability_score_thresh)
                {
                    mfinalMask.Add(mMask.GetRange(j * mShape[2] * mShape[3], mShape[2] * mShape[3]));
                    //m.AddRange(this.mMask.GetRange(j * this.mShape[2] * this.mShape[3], this.mShape[2] * this.mShape[3]));
                    i.Add(mIoU[j]);
                    s.Add(mStalibility[j]);
                    batch++;
                }              
            }
            mShape[1] = batch;
            mStalibility.Clear();
            mMask.Clear();
            mIoU.Clear();
            //this.mMask.AddRange(m);
            mIoU.AddRange(i);
            mStalibility.AddRange(s);
        }
       

        public float[] CalculateStabilityScore(float maskThreshold, float thresholdOffset)
        {
            var batchSize = mShape[1];
            var width = mShape[3];
            var height = mShape[2];

            var intersections = new float[batchSize];
            var unions = new float[batchSize];

            for (var i = 0; i < batchSize; i++)
            {
                float intersectionSum = 0;
                float unionSum = 0;

                for (var j = 0; j < width; j++)
                {
                    for (var k = 0; k < height; k++)
                    {
                        var index = i * width * height + k * width + j;
                        if (mMask[index] > maskThreshold + thresholdOffset)
                        {
                            intersectionSum++;
                        }
                        if (mMask[index] > maskThreshold - thresholdOffset)
                        {
                            unionSum++;
                        }
                    }
                }

                intersections[i] = intersectionSum;
                unions[i] = unionSum;
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
            mShape[0] = md.mShape[0];
            mShape[1] += md.mShape[1];
            mShape[2] = md.mShape[2];
            mShape[3] = md.mShape[3];
            mBox.AddRange(md.mBox);
            mMask.AddRange(md.mMask);
            mStalibility.AddRange(md.mStalibility);
            mIoU.AddRange(md.mIoU);

            mfinalMask.AddRange(md.mfinalMask);
        }

        public int[] batched_mask_to_box()
        {
            var C = mShape[1];
            var width = mShape[3];
            var height = mShape[2];

            var boxes = new int[C*4];

            for (var c = 0; c < C; c++)
            {
                var emptyMask = true;
                var top = height;
                var bottom = 0;
                var left = width;
                var right = 0;

                for (var i = 0; i < width; i++)
                {
                    for (var j = 0; j < height; j++)
                    {
                        //int index = c * width * height + j * width + i;
                        //if (this.mMask[index] > 0)
                        var index =j * width + i;
                        if (mfinalMask[c][index] > 0)
                        {
                            emptyMask = false;
                            top = Math.Min(top, j);
                            bottom = Math.Max(bottom, j);
                            left = Math.Min(left, i);
                            right = Math.Max(right, i);
                        }
                    }
                }

                if (emptyMask)
                {
                    boxes[c*4]=0;
                    boxes[c * 4+1] = 0;
                    boxes[c * 4+2] = 0;
                    boxes[c * 4+3] = 0;
                }
                else
                {
                    boxes[c * 4] = left;
                    boxes[c * 4 + 1] = top;
                    boxes[c * 4 + 2] = right;
                    boxes[c * 4 + 3] = bottom;
                }
            }

            return boxes;
        }
    }
}
