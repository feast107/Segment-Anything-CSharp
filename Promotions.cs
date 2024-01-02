using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public UserControl mAnation;
        public abstract float[] GetInput();
        public abstract float[] GetLable();
        public PromotionType mType;
    }
    /// <summary>
    /// 提示点
    /// </summary>
    public class PointPromotion: Promotion
    {

        public PointPromotion(OpType optype)
        {
            mType = PromotionType.Point;
            m_Optype = optype;
        }
        public int X { get; set; }
        public int Y { get; set; }
        public override float[] GetInput()
        {
            return new float[2] { X ,Y};
        }
        public override float[] GetLable()
        {
            if (m_Optype == OpType.ADD)
            {
                return new float[1] { 1 };
            }
            else
            {
                return new float[1] { 0 };
            }          
        }

        public OpType m_Optype;
    }
    public enum OpType
    {
        ADD,
        REMOVE
    }
    /// <summary>
    /// 提示框
    /// </summary>
    internal class BoxPromotion : Promotion
    {
        public BoxPromotion()
        {
            mLeftUp = new PointPromotion(OpType.ADD);
            mRightBottom = new PointPromotion(OpType.ADD);
            mType = PromotionType.Box;
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
