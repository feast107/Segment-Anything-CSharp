using System.Windows.Controls;

namespace SAMViewer
{
    public enum PromotionType
    {
        Point,
        Box
    }

    public abstract class Promotion
    {
        public UserControl Anation;
        public abstract float[] GetInput();
        public abstract float[] GetLable();
        public PromotionType Type;
    }
    /// <summary>
    /// 提示点
    /// </summary>
    public class PointPromotion: Promotion
    {

        public PointPromotion(OpType optype)
        {
            Type = PromotionType.Point;
            Optype = optype;
        }
        public int X { get; set; }
        public int Y { get; set; }
        public override float[] GetInput()
        {
            return new float[2] { X ,Y};
        }
        public override float[] GetLable()
        {
            if (Optype == OpType.Add)
            {
                return new float[1] { 1 };
            }
            else
            {
                return new float[1] { 0 };
            }          
        }

        public OpType Optype;
    }
    public enum OpType
    {
        Add,
        Remove
    }
    /// <summary>
    /// 提示框
    /// </summary>
    internal class BoxPromotion : Promotion
    {
        public BoxPromotion()
        {
            mLeftUp = new PointPromotion(OpType.Add);
            mRightBottom = new PointPromotion(OpType.Add);
            Type = PromotionType.Box;
        }
        public override float[] GetInput()
        {
            return new float[4] { mLeftUp.X, 
                mLeftUp.Y, 
                mRightBottom.X, 
                mRightBottom.Y };
        }
        public override float[] GetLable()
        {
            return new float[2] { 2,3 };
        }
        public PointPromotion mLeftUp { get; set; }//左上角点
        public PointPromotion mRightBottom { get; set; }//左上角点

    }
    /// <summary>
    /// 提示蒙版
    /// </summary>
    internal class MaskPromotion
    {
        public MaskPromotion(int wid,int hei)
        {
            mWidth = wid;
            mHeight = hei;
            mMask = new float[mWidth,mHeight];
        }

        private float[,] mMask   { get; set; } //蒙版
        public  int      mWidth  { get; set; } //长度
        public  int      mHeight { get; set; } //高度
    }
    /// <summary>
    /// 提示文本
    /// </summary>
    internal class TextPromotion
    {
        public string mText { get; set; }
    }
}
