using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SAMViewer
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        // 图像文件路径
        private          string          imagePath     = string.Empty;
        private readonly Sam             sam           = Sam.Instance();
        private readonly Clip            clip          = SAMViewer.Clip.Instance();
        private readonly List<Promotion> promotionList = new();
        private          float[]         imgEmbedding;
        private          RectAnnotation  curRectAnno;
        private          Point           startPoint;
        private          int             orgwid;

        private int orghei;
        //undo and redo
        private readonly Stack<Promotion> undoStack = new();
        private readonly Stack<Promotion> redoStack = new();
        private readonly Dispatcher       ui;
        private          SamAutoMask      autoMask;
        private          MaskData         autoMaskData;

        private Operation curOp;
        // 构造函数
        public MainWindow()
        {
            InitializeComponent();

            mImage.Width = 0.7f * Width;
            mImage.Height = Height;

            mMask.Width = 0.7f * Width;
            mMask.Height = Height;

            ui = Dispatcher.CurrentDispatcher;

        }

        /// <summary>
        /// 加载图像
        /// </summary>
        private void LoadImage(string imgpath)
        {
            var bitmap = new BitmapImage(new Uri(imgpath));
            orgwid = (int)bitmap.Width;
            orghei = (int)bitmap.Height;
            mImage.Source = bitmap;//显示图像            
        }
        // 鼠标左键按下事件处理程序
        private void image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 如果当前没有选中的标注，创建一个点标注
            mImage.CaptureMouse();
            switch (curOp)
            {
                case Operation.Point:
                {
                    var type  = RAdd.IsChecked == true ? OpType.Add : OpType.Remove;
                    var brush = type           == OpType.Add ? Brushes.Red : Brushes.Black;

                    var annotation = new PointAnnotation(brush);
                    var canvasP    = e.GetPosition(ImgCanvas);
                    annotation.Position = canvasP;
                    ImgCanvas.Children.Add(annotation);

               
                    Promotion promt       = new PointPromotion(type);
                    var       clickPoint  = e.GetPosition(mImage);
                    var       orgImgPoint = Window2Image(clickPoint);
                    ((PointPromotion)promt).X = (int)orgImgPoint.X;
                    ((PointPromotion)promt).Y = (int)orgImgPoint.Y;
             
              
                    var ts  = new Transforms(1024);
                    var ptn = ts.ApplyCoords((promt as PointPromotion)!, orgwid, orghei);
                    ptn.Anation = annotation;
                    undoStack.Push(ptn);
                    promotionList.Add(ptn);
                    var thread = new Thread(() =>
                    {
                        var md = sam.Decode(promotionList, imgEmbedding, orgwid, orghei);
                        ShowMask(md.Mask.ToArray(), Color.FromArgb(100, 255, 0, 0));
                    });
                    thread.Start();
                    break;
                }
                case Operation.Box:
                {
                    startPoint = e.GetPosition(ImgCanvas);
                    curRectAnno = new RectAnnotation
                    {
                        Width         = 0,
                        Height        = 0,
                        StartPosition = startPoint
                    };
                    Reset();
                    ImgCanvas.Children.Add(curRectAnno);

                    var clickPoint  = e.GetPosition(mImage);
                    var orgImgPoint = Window2Image(clickPoint);
                    curRectAnno.LeftUp = orgImgPoint;
                    break;
                }
            }
        }

        // 鼠标移动事件处理程序
        private void image_MouseMove(object sender, MouseEventArgs e)
        {
            // 如果当前有选中的标注，处理拖动和调整大小操作
            if (e.LeftButton != MouseButtonState.Pressed || curRectAnno == null) return;
            var currentPoint = e.GetPosition(ImgCanvas);
            var width        = Math.Abs(currentPoint.X - startPoint.X);
            var height       = Math.Abs(currentPoint.Y - startPoint.Y);
            curRectAnno.Width  = width;
            curRectAnno.Height = height;
            Canvas.SetLeft(curRectAnno, Math.Min(startPoint.X, currentPoint.X));
            Canvas.SetTop(curRectAnno, Math.Min(startPoint.Y, currentPoint.Y));
        }
        private void image_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            mImage.ReleaseMouseCapture();
            if (curRectAnno == null)
                return;
        
            var clickPoint = e.GetPosition(mImage);
            var orgImgPoint = Window2Image(clickPoint);
            curRectAnno.RightBottom = orgImgPoint;

            var promt = new BoxPromotion
            {
                mLeftUp =
                {
                    X = (int)curRectAnno.LeftUp.X,
                    Y = (int)curRectAnno.LeftUp.Y
                },
                mRightBottom =
                {
                    X = (int)curRectAnno.RightBottom.X,
                    Y = (int)curRectAnno.RightBottom.Y
                }
            };

            var ts = new Transforms(1024);
            var pb = ts.ApplyBox(promt, orgwid, orghei);
            pb.Anation = curRectAnno;
            undoStack.Push(pb);
            promotionList.Add(pb);
            var thread = new Thread(() =>
            {
                var md = sam.Decode(promotionList, imgEmbedding,orgwid,orghei);
                ShowMask(md.Mask.ToArray(), Color.FromArgb(100, 255, 0, 0));
            });
            thread.Start();
            curRectAnno = null;
        }
       
        /// <summary>
        /// 图像路径选择
        /// </summary>
        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                // Set filter for file extension and default file extension
                DefaultExt = ".png",
                Filter     = "Image files (*.jpg, *.jpeg, *.png)|*.jpg;*.jpeg;*.png|All files (*.*)|*.*"
            };

            // Display OpenFileDialog by calling ShowDialog method
            var result = openFileDialog.ShowDialog();

            // Get the selected file name and display in a TextBox
            if (result == true)
            {
                ImgPathTxt.Text = openFileDialog.FileName;
                imagePath = ImgPathTxt.Text;

                if (!File.Exists(imagePath))
                    return;

                LoadImgGrid.Visibility = Visibility.Collapsed;
                ImgCanvas.Visibility = Visibility.Visible;
                LoadImage(imagePath);
                ShowStatus("Image Loaded");

                var thread = new Thread(() =>
                {
                    sam.LoadOnnxModel();//加载Segment Anything模型

                    ui.Invoke(new Action(delegate
                    {
                        ShowStatus("ONNX Model Loaded");
                    }));
                    // 读取图像
                    var image = OpenCvSharp.Cv2.ImRead(imagePath);
                    imgEmbedding = sam.Encode(image, orgwid, orghei);//Image Embedding

                    autoMask               = new SamAutoMask
                    {
                        ImgEmbedding = imgEmbedding,
                        Sam          = sam
                    };
                    image.Dispose();
                    ui.Invoke(new Action(delegate
                    {
                        ShowStatus("Image Embedding Cal Finished");
                    }));
                });
                thread.Start();

            }
        }
        private void BReLoad_Click(object sender, RoutedEventArgs e)
        {
            Reset();
            LoadImgGrid.Visibility = Visibility.Visible;
            ImgCanvas.Visibility = Visibility.Hidden;
        }

        private double CalculateCosineSimilarity(List<float> vector1, List<float> vector2)
        {
            var dotProduct = DotProduct(vector1, vector2);
            var magnitude1 = Magnitude(vector1);
            var magnitude2 = Magnitude(vector2);

            if (magnitude1 == 0 || magnitude2 == 0)
                return 0;

            return dotProduct / (magnitude1 * magnitude2);
        }

        private double DotProduct(List<float> vector1, List<float> vector2)
        {          
            return vector1.Zip(vector2, (a, b) => a * b).Sum();
        }

        private static double Magnitude(List<float> vector)
        {
            return Math.Sqrt(vector.Select(x => x * x).Sum());
        }
        /// <summary>
        /// 撤销
        /// </summary>
        private void BUndo_Click(object sender, RoutedEventArgs e)
        {
            if (undoStack.Count > 0)
            {
                var p = undoStack.Pop();
                redoStack.Push(p);
                RemoveAnnotation(p);
                promotionList.Clear();
                promotionList.AddRange(undoStack.ToArray());
                
                var thread = new Thread(() =>
                {
                    var md = sam.Decode(promotionList, imgEmbedding, orgwid, orghei);
                    ShowMask(md.Mask.ToArray(), Color.FromArgb(100, 255, 0, 0));
                });
                thread.Start();
            }
            else
            {
                MessageBox.Show("No Undo Promot");
            }
        }
        /// <summary>
        /// 重做
        /// </summary>
        private void BRedo_Click(object sender, RoutedEventArgs e)
        {
            if (redoStack.Count > 0)
            {
                var pt = redoStack.Pop();
                undoStack.Push(pt);
                AddAnnotation(pt);
                promotionList.Clear();
                promotionList.AddRange(undoStack.ToArray());
                var thread = new Thread(() =>
                {
                    var md = sam.Decode(promotionList, imgEmbedding, orgwid, orghei);
                    ShowMask(md.Mask.ToArray(), Color.FromArgb(100, 255, 0, 0));
                });
                thread.Start();
            }
            else
            {
                MessageBox.Show("No Redo Promot");
            }
        }
        /// <summary>
        /// 复位
        /// </summary>
        private void BReset_Click(object sender, RoutedEventArgs e)
        {
            Reset();
        }
        /// <summary>
        /// 显示分割结果
        /// </summary>
        private void ShowMask(float[] mask, Color color)
        {

            ui.Invoke(new Action(delegate
            {
                var bp = new WriteableBitmap(orgwid, orghei, 96, 96, PixelFormats.Pbgra32, null);
                // 设置像素数据，将所有像素的透明度设置为半透明
                var pixelData = new byte[orgwid * orghei * 4];
                Array.Clear(pixelData, 0, pixelData.Length);
                for (var y = 0; y < orghei; y++)
                {
                    for (var x = 0; x < orgwid; x++)
                    {
                        var ind = y * orgwid + x;
                        if (!(mask[ind] > sam.MaskThreshold)) continue;
                        pixelData[4 * ind]     = color.B; // Blue
                        pixelData[4 * ind + 1] = color.G; // Green
                        pixelData[4 * ind + 2] = color.R; // Red
                        pixelData[4 * ind + 3] = 100;     // Alpha
                    }
                }

                bp.WritePixels(new Int32Rect(0, 0, orgwid, orghei), pixelData, orgwid * 4, 0);
                // 创建一个BitmapImage对象，将WriteableBitmap作为源
                mMask.Source = bp;
            }));
        }

        /// <summary>
        /// 显示分割结果
        /// </summary>
        private void ShowMask(MaskData mask)
        {
            ui.Invoke(() =>
            {
                ShowStatus("Finish");
                ClearAnnotation();
                var bp = new WriteableBitmap(orgwid, orghei, 96, 96, PixelFormats.Pbgra32, null);
                // 设置像素数据，将所有像素的透明度设置为半透明
                var pixelData = new byte[orgwid * orghei * 4];
                Array.Clear(pixelData, 0, pixelData.Length);
                for (var i = 0; i < mask.Shape[1]; i++)
                {
                    var random = new Random();
                    var randomColor = Color.FromArgb(100, (byte)random.Next(256), (byte)random.Next(256),
                        (byte)random.Next(256));
                    for (var y = 0; y < orghei; y++)
                    {
                        for (var x = 0; x < orgwid; x++)
                        {
                            //int ind = i* this.mOrghei* this.mOrgwid+y * this.mOrgwid + x;
                            //int indpixel = y * this.mOrgwid + x;
                            //if (mask.mMask[ind] > this.mSam.mask_threshold)

                            var indpixel = y * orgwid + x;
                            if (mask.MfinalMask[i][indpixel] > sam.MaskThreshold)
                            {
                                pixelData[4 * indpixel]     = randomColor.B; // Blue
                                pixelData[4 * indpixel + 1] = randomColor.G; // Green
                                pixelData[4 * indpixel + 2] = randomColor.R; // Red
                                pixelData[4 * indpixel + 3] = 100;           // Alpha
                            }
                        }
                    }

                    var leftup    = Image2Window(new Point(mask.Box[4 * i], mask.Box[4 * i + 1]));
                    var rightdown = Image2Window(new Point(mask.Box[4                  * i + 2], mask.Box[4 * i + 3]));
                    var box       = new RectAnnotation();
                    ImgCanvas.Children.Add(box);
                    box.Width  = rightdown.X - leftup.X;
                    box.Height = rightdown.Y - leftup.Y;
                    Canvas.SetLeft(box, leftup.X);
                    Canvas.SetTop(box, leftup.Y);
                }

                bp.WritePixels(new Int32Rect(0, 0, orgwid, orghei), pixelData, orgwid * 4, 0);
                // 创建一个BitmapImage对象，将WriteableBitmap作为源
                mMask.Source = bp;
                var rtbitmap = new RenderTargetBitmap(bp.PixelWidth, 
                    bp.PixelHeight, 
                    bp.DpiX, 
                    bp.DpiY, 
                    PixelFormats.Default);
                var drawingVisual = new DrawingVisual();
                using (var dc = drawingVisual.RenderOpen())
                {
                    dc.DrawImage(bp, new Rect(0, 0, bp.Width, bp.Height));
                }
                rtbitmap.Render(drawingVisual);
                var bitmapEncoder = new JpegBitmapEncoder();
                bitmapEncoder.Frames.Add(BitmapFrame.Create(rtbitmap));
                var strDir  = Path.Combine(AppContext.BaseDirectory , "Output");
                var strpath = Path.Combine(strDir, DateTime.Now.ToString("yyyyMMddfff") + ".jpg");
                if (!Directory.Exists(strDir))
                {
                    Directory.CreateDirectory(strDir);
                }
                if (!File.Exists(strpath))
                {
                    using var stream = File.OpenWrite(strpath);
                    bitmapEncoder.Save(stream);
                }
            });
        }
        /// <summary>
        /// 窗口坐标转图像坐标
        /// </summary>
        private Point Window2Image(Point clickPoint)
        {
            var imageWidth  = mImage.ActualWidth;
            var imageHeight = mImage.ActualHeight;
            var scaleX      = imageWidth                        / orgwid;
            var scaleY      = imageHeight                       / orghei;
            var offsetX     = (imageWidth   - scaleX * orgwid) / 2;
            var offsetY     = (imageHeight  - scaleY * orghei) / 2;
            var imageX      = (clickPoint.X - offsetX)          / scaleX;
            var imageY      = (clickPoint.Y - offsetY)          / scaleY;
            var p           = new Point
            {
                X = (int)imageX,
                Y = (int)imageY
            };

            return p;
        }

        private Point Image2Window(Point image)
        {
            var imageWidth = mImage.ActualWidth;
            var imageHeight = mImage.ActualHeight;
            var scaleX = imageWidth / orgwid;
            var scaleY = imageHeight / orghei;
            var offsetX = (imageWidth - scaleX * orgwid) / 2;
            var offsetY = (imageHeight - scaleY * orghei) / 2;

            var windowsX = image.X * scaleX + offsetX;
            var windowsY = image.Y * scaleY + offsetX;

            var p = new Point
            {
                X = (int)windowsX,
                Y = (int)windowsY
            };

            return p;
        }
        /// <summary>
        /// 清空
        /// </summary>
        private void ClearAnnotation()
        {
            var todel = (from object v in ImgCanvas.Children
                where v is PointAnnotation || v is RectAnnotation
                select v as UserControl).ToList();

            todel.ForEach(e => { ImgCanvas.Children.Remove(e); });
        }
        /// <summary>
        /// 删除
        /// </summary>
        private void RemoveAnnotation(Promotion pt)
        {
            if (ImgCanvas.Children.Contains(pt.Anation))
                ImgCanvas.Children.Remove(pt.Anation);
        }
        /// <summary>
        /// 添加
        /// </summary>
        private void AddAnnotation(Promotion pt)
        {
            if (!ImgCanvas.Children.Contains(pt.Anation))
                ImgCanvas.Children.Add(pt.Anation);

        }
        /// <summary>
        /// 显示状态信息
        /// </summary>
        private void ShowStatus(string message)
        {
            StatusTxt.Text = message;
        }

        private void Reset()
        {
            ClearAnnotation();
            promotionList.Clear();
            mMask.Source = null;
        }

        private void mAddPoint_Click(object sender, RoutedEventArgs e)
        {
            curOp = Operation.Point;
        }

        private void mAddBox_Click(object sender, RoutedEventArgs e)
        {
            curOp = Operation.Box;
        }

        private void mAutoSeg_Click(object sender, RoutedEventArgs e)
        {
            autoMask.PointsPerSide = int.Parse(mPoints_per_side.Text);
            autoMask.PredIouThresh = float.Parse(mPred_iou_thresh.Text);
            autoMask.StabilityScoreThresh = float.Parse(mStability_score_thresh.Text);
            ShowStatus("Auto Segment......");
            var thread = new Thread(() =>
            {
                curOp = Operation.Everything;               
                autoMaskData = autoMask.Generate(imagePath);
                ShowMask(autoMaskData);
            });
            thread.Start();        
        }

        private MaskData MatchTextAndImage(string txt)
        {
            var txtEmbedding = clip.TxtEncoder(txt);
            var image = new OpenCvSharp.Mat(imagePath);
            var maxIndex = 0;
            var max = 0.0;
            var final = new MaskData();
            for (var i = 0; i < autoMaskData.Shape[1]; i++)
            {
                // Define the coordinates of the ROI
                var x = autoMaskData.Box[4 * i];  // Top-left x coordinate
                var y = autoMaskData.Box[4 * i + 1];// Top-left y coordinate
                var width = autoMaskData.Box[4 * i + 2] - autoMaskData.Box[4 * i];  // Width of the ROI
                var height = autoMaskData.Box[4 * i + 3] - autoMaskData.Box[4 * i + 1];  // Height of the ROI

                // Create a Rect object for the ROI
                var roiRect = new OpenCvSharp.Rect(x, y, width, height);
                // Extract the ROI from the image
                var roi = new OpenCvSharp.Mat(image, roiRect);
                var neww = 0;
                var newh = 0;
                var scale = 224 * 1.0f / Math.Max(image.Rows, image.Cols);
                var newht = image.Rows * scale;
                var newwt = image.Cols * scale;

                neww = (int)(newwt + 0.5);
                newh = (int)(newht + 0.5);

                var resizedImage = new OpenCvSharp.Mat();
                OpenCvSharp.Cv2.Resize(roi, resizedImage, new OpenCvSharp.Size(neww, newh));
                // 创建大的Mat
                var largeMat = new OpenCvSharp.Mat(new OpenCvSharp.Size(224, 224), OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.Black);

                // 计算小的Mat放置的位置
                var xoffset = (largeMat.Width - resizedImage.Width) / 2;
                var yoffset = (largeMat.Height - resizedImage.Height) / 2;

                // 将小的Mat放置到大的Mat的中心位置
                resizedImage.CopyTo(largeMat[new OpenCvSharp.Rect(xoffset, yoffset, resizedImage.Width, resizedImage.Height)]);

                //将图像转换为浮点型
                var floatImage = new OpenCvSharp.Mat();
                largeMat.ConvertTo(floatImage, OpenCvSharp.MatType.CV_32FC3);
                // 计算均值和标准差
                var mean = new OpenCvSharp.Scalar(0.48145466, 0.4578275, 0.40821073);
                var std = new OpenCvSharp.Scalar(0.26862954, 0.26130258, 0.27577711);
                // 归一化
                OpenCvSharp.Cv2.Normalize(floatImage, floatImage, 0, 255, OpenCvSharp.NormTypes.MinMax);
                OpenCvSharp.Cv2.Subtract(floatImage, mean, floatImage);
                OpenCvSharp.Cv2.Divide(floatImage, std, floatImage);

                var transformedImg = new float[3 * 224 * 224];
                for (var ii = 0; ii < 224; ii++)
                {
                    for (var j = 0; j < 224; j++)
                    {
                        var index = j * 224 + ii;
                        transformedImg[index] = floatImage.At<OpenCvSharp.Vec3f>(j, ii)[0];
                        transformedImg[224 * 224 + index] = floatImage.At<OpenCvSharp.Vec3f>(j, ii)[1];
                        transformedImg[2 * 224 * 224 + index] = floatImage.At<OpenCvSharp.Vec3f>(j, ii)[2];
                    }
                }

                var imgEmbedding = clip.ImgEncoder(transformedImg);
                var maxs = CalculateCosineSimilarity(txtEmbedding.ToList(), imgEmbedding.ToList());
                if (maxs > max)
                {
                    maxIndex = i;
                    max = maxs;
                }

                roi.Dispose();
                resizedImage.Dispose();
                largeMat.Dispose();
                floatImage.Dispose();
            }

            autoMaskData.Shape.CopyTo(final.Shape,0);
            final.Shape[1] = 1;
            final.Box.AddRange(autoMaskData.Box.GetRange(maxIndex * 4, 4));
            final.IoU.AddRange(autoMaskData.IoU.GetRange(maxIndex, 1));
            final.Stalibility.AddRange(autoMaskData.Stalibility.GetRange(maxIndex, 1));
            //.GetRange(maxindex * final.mShape[2] * final.mShape[3], final.mShape[2] * final.mShape[3])
            final.MfinalMask.Add(autoMaskData.MfinalMask[maxIndex]);



            image.Dispose();


            return final;
        }
        private void mText_Click(object sender, RoutedEventArgs e)
        {
            curOp = Operation.Text;
            ShowStatus("Image And Text Matching......");
            var txt = mTextinput.Text;
            var thread = new Thread(() =>
            {
                var matches = MatchTextAndImage(txt);
                ShowMask(matches);
            });
            thread.Start();
        }

        private void Expanded(object sender, RoutedEventArgs e)
        {
            if (mPointexp == null || mBoxexp == null || mEverythingExp == null || mTextExp == null)
                return;

            var exp = sender as Expander;
            if (exp?.IsExpanded != true) return;
            mPointexp.IsExpanded      = mPointexp      == exp;
            mBoxexp.IsExpanded        = mBoxexp        == exp;
            mEverythingExp.IsExpanded = mEverythingExp == exp;
            mTextExp.IsExpanded       = mTextExp       == exp;

        }
    }

    internal enum Operation
    {
        Point,
        Box,
        Everything,
        Text
    }

}