using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Basler.Pylon;
using HalconDotNet;


namespace Basler
{
    public class Basler
    {
        #region 定义变量
        HObject ho_image = null;
        Camera camera = null;
        Dictionary<string, string> dicCameraInfo = new Dictionary<string, string>();//将相机用户ID和序列号绑定
        List<ICameraInfo> allCameras = null;
        const int cTimeOutMs = 20000; //相机连接超时时间
        IGrabResult grabResult = null;

        #endregion

        /// <summary>
        /// 初始化设备上所有的相机信息
        /// </summary>
        public bool Init()
        {
            try
            {
                allCameras = CameraFinder.Enumerate(); // 查找设备上所有的相机
                if (allCameras.Count == 0)
                {
                    return false;
                }
                //遍历所有相机
                foreach (ICameraInfo cameraInfo in allCameras)
                {
                    dicCameraInfo.Add(cameraInfo[CameraInfoKey.UserDefinedName], cameraInfo[CameraInfoKey.SerialNumber]);                
                }
            }
            catch(Exception e)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// 连接相机
        /// </summary>
        /// <param name="UserId">设备用户ID</param>
        /// <returns>是否连接成功</returns>
        public bool CameraConnnection(string UserId)
        {
            try
            {   
                camera = new Camera(dicCameraInfo[UserId]);

                camera.CameraOpened += Configuration.AcquireContinuous; // Configuration.
                camera.ConnectionLost += Camera_ConnectionLost;
                camera.Open();
                camera.Parameters[PLTransportLayer.HeartbeatTimeout].TrySetValue(1000, IntegerValueCorrection.Nearest); //解决异常退出时重新打开相机失败
                // 参数MaxNumBuffer可用于抓怕分配的控制缓冲区数量。这个参数的默认值是10。
                camera.Parameters[PLCameraInstance.MaxNumBuffer].SetValue(10);
                // 自由采集模式
                camera.Parameters[PLCamera.TriggerMode].SetValue(PLCamera.TriggerMode.Off);
            }
            catch(Exception e)
            {
                camera = null;
                return false;
            }
            return true;
        }

        /// <summary>
        /// 相机断开连接异常中断函数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Camera_ConnectionLost(object sender, EventArgs e)
        {
            try
            {
                if (!camera.IsConnected)
                {
                    camera.Close();
                    camera.Open(cTimeOutMs, TimeoutHandling.ThrowException);
                    camera.Parameters[PLTransportLayer.HeartbeatTimeout].TrySetValue(1000, IntegerValueCorrection.Nearest);
                }
            }
            catch (Exception err)
            {
                // MessageBox.Show(err.Message);
                return;
            }
        }

        /// <summary>
        /// 保存图像
        /// </summary>
        /// <param name="ho_Image"></param>
        /// <param name="filePath">图像路径</param>
        /// <returns></returns>
        public bool SaveImage(HObject ho_image,string filePath)
        {
            try
            {
                if (grabResult.GrabSucceeded)
                {
                    //ImagePersistence.Save(ImageFileFormat.Bmp, filePath, grabResult); //存储图像到本地
                    HOperatorSet.WriteImage(ho_image, "bmp", 0, filePath);
                }
            }
            catch(Exception e)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// 拍照
        /// </summary>
        /// <param name="UserId"></param>
        /// <param name="ho_image"></param>
        /// <returns></returns>
        public bool GrabImage(string UserId)
        {
            try
            {   camera.StreamGrabber.ImageGrabbed += OnImageGrabbed; // 中断函数
                camera.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
            }
            catch(Exception e)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 中断响应函数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnImageGrabbed(Object sender, ImageGrabbedEventArgs e)
        {
            try
            {
                grabResult = e.GrabResult;
                if (grabResult.GrabSucceeded)
                {
                    byte[] buffer = grabResult.PixelData as byte[];
                    unsafe
                    {    //C# SDK Basler图像变量(IGrabResult)转换为 Halcon图像变量(HObject)
                        fixed (byte* p = buffer)
                        {
                            HOperatorSet.GenImage1(out ho_image, "byte", grabResult.Width, grabResult.Height, new IntPtr(p));
                            //HOperatorSet.SetPart(hWindowImage.HalconWindow, 0, 0, grabResult.Height - 1, grabResult.Width - 1); //显示尺寸自适应
                            //HOperatorSet.DispObj(ho_image, hWindowImage.HalconWindow);
                        }
                    }
                    //HOperatorSet.WriteImage(ho_Image, "bmp", 0, filePath);
                    //ImagePersistence.Save(ImageFileFormat.Bmp, filePath, grabResult); //存储图像到本地
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err.Message);
            }

        }
        /// <summary>
        /// 断开相机连接
        /// </summary>
        /// <param name="UserId"></param>
        /// <returns></returns>
        public bool CameraDisConnection(string UserId)
        {
            try
            {
                //if (UserId == camera.CameraInfo[CameraInfoKey.UserDefinedName])
                //{
                if (camera.IsConnected)
                {
                    camera.StreamGrabber.Stop();
                    camera.Close();
                    camera.Dispose();
                    if(null != ho_image)
                    {
                        ho_image.Dispose();
                    }
                }
                //}
            }
            catch(Exception e)
            {
                return false;
            }

            return true;
        }


    }
}
