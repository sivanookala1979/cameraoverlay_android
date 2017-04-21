using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Util;
using Android;
using System.Collections.Generic;
using CameraOverlay.Droid;
using Android.Media;
using Java.Lang;
using Android.Content.Res;
using Java.IO;
using Java.Nio;
using Android.Provider;
using CameraOverlay.Droid.Listeners;
namespace CameraOverlay.Droid
{
	[Activity(Label = "CameraOverlay.Droid", MainLauncher = true, Icon = "@drawable/icon")]
	public class MainActivity : Activity, TextureView.ISurfaceTextureListener
	{
		int count = 1;
		TextureView textureView;
		TextView overLayView;
		Button takePictureButton;
		private string cameraId;
		private Size imageDimension;
		private static int REQUEST_CAMERA_PERMISSION = 200;
		public CameraDevice cameraDevice;
		public CameraCaptureSession cameraCaptureSessions;
		private ImageReader imageReader;

		protected CaptureRequest.Builder captureRequestBuilder;
		private HandlerThread mBackgroundThread;
		public Handler mBackgroundHandler;
		int deviceHeight, deviceWidth;
		public CaptureRequest.Builder captureBuilder;
		ImageReader.IOnImageAvailableListener mOnImageAvailableListener;

		private static readonly SparseIntArray ORIENTATIONS = new SparseIntArray();
		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);

			// Set our view from the "main" layout resource
			SetContentView(Resource.Layout.Main);

			deviceWidth = getScreenWidth();
			deviceHeight = getScreenHeight();



			textureView = FindViewById<TextureView>(Resource.Id.texture);
			overLayView = FindViewById<TextView>(Resource.Id.overlayView);
			textureView.SurfaceTextureListener = this;
			takePictureButton = FindViewById<Button>(Resource.Id.btn_takepicture);
			takePictureButton.Click += TakePictureButton_Click;
		}

		private void TakePictureButton_Click(object sender, EventArgs e)
		{
			Toast.MakeText(this, "Saved file to location " + this.GetExternalFilesDir(Android.OS.Environment.DirectoryDcim), ToastLength.Long).Show();
			TakePicture();
		}

		public int getScreenWidth()
		{
			return Resources.DisplayMetrics.WidthPixels;

		}


		public int getScreenHeight()
		{
			return Resources.DisplayMetrics.HeightPixels;
		}

		protected void TakePicture()
		{
			if (null == cameraDevice)
			{
				return;
			}
			CameraManager manager = (CameraManager)GetSystemService(Context.CameraService);
			try
			{
				System.Console.WriteLine("1");
				CameraCharacteristics characteristics = manager.GetCameraCharacteristics(cameraDevice.Id);
				Size[] jpegSizes = null;
				System.Console.WriteLine("2");
				if (characteristics != null)
				{
					StreamConfigurationMap map = (StreamConfigurationMap)characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap);


					jpegSizes = map.GetOutputSizes((int)ImageFormatType.Jpeg);

				}
				System.Console.WriteLine("3");

				int width = deviceWidth;
				int height = deviceHeight;
				if (jpegSizes != null && 0 < jpegSizes.Length)
				{
					width = jpegSizes[0].Width;
					height = jpegSizes[0].Height;
				}
				System.Console.WriteLine("4");
				ImageReader reader = ImageReader.NewInstance(width, height, ImageFormatType.Jpeg, 1);
				List<Surface> outputSurfaces = new List<Surface>(2);
				outputSurfaces.Add(reader.Surface);
				outputSurfaces.Add(new Surface(textureView.SurfaceTexture));
				captureBuilder = cameraDevice.CreateCaptureRequest(CameraTemplate.StillCapture);
				System.Console.WriteLine("5");
				captureBuilder.AddTarget(reader.Surface);
				System.Console.WriteLine("5.1");
				captureBuilder.Set(CaptureRequest.ControlMode, (int)ControlMode.Auto);
				System.Console.WriteLine("5.2");
				int rotation = (int)WindowManager.DefaultDisplay.Rotation;
				System.Console.WriteLine("5.3");
				mOnImageAvailableListener = new SavePhoto(this);
				System.Console.WriteLine("5.4");
				reader.SetOnImageAvailableListener(mOnImageAvailableListener, mBackgroundHandler);
				System.Console.WriteLine("6");
				cameraDevice.CreateCaptureSession(outputSurfaces, new CaptureCallback(this, true), mBackgroundHandler);
			}

			catch (System.Exception ex)
			{
				System.Console.WriteLine("Exception occurred 137");
				System.Console.WriteLine(ex.ToString());
				// throw ex;
			}

		}
		protected override void OnPause()
		{

			base.OnPause();
			CloseCamera();
			stopBackgroundThread();

		}
		private void CloseCamera()
		{
			if (null != cameraDevice)
			{
				cameraDevice.Close();
				cameraDevice = null;
			}
			if (null != imageReader)
			{
				imageReader.Close();
				imageReader = null;
			}
		}

		protected void startBackgroundThread()
		{
			mBackgroundThread = new HandlerThread("Camera Background");
			mBackgroundThread.Start();
			mBackgroundHandler = new Handler(mBackgroundThread.Looper);
		}
		protected void stopBackgroundThread()
		{
			mBackgroundThread.QuitSafely();
			try
			{
				mBackgroundThread.Join();
				mBackgroundThread = null;
				mBackgroundHandler = null;
			}
			catch (InterruptedException e)
			{
				//e.printStackTrace();
			}
		}
		protected override void OnStart()
		{
			base.OnStart();
			//startBackgroundThread();
		}
		protected override void OnResume()
		{
			base.OnResume();
			startBackgroundThread();
			if (textureView.IsAvailable)
			{
				OpenCamera();
			}
			else
			{
				textureView.SurfaceTextureListener = this;
			}
		}

		public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height)
		{
			OpenCamera();
		}

		public bool OnSurfaceTextureDestroyed(SurfaceTexture surface)
		{

			return false;//throw new NotImplementedException();
		}

		public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height)
		{
			//throw new NotImplementedException();
		}

		public void OnSurfaceTextureUpdated(SurfaceTexture surface)
		{
			//throw new NotImplementedException();
		}
		private void OpenCamera()
		{
			CameraManager manager = (CameraManager)GetSystemService(Context.CameraService);

			try
			{
				cameraId = manager.GetCameraIdList()[0];
				CameraCharacteristics characteristics = manager.GetCameraCharacteristics(cameraId);
				StreamConfigurationMap map = (StreamConfigurationMap)characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap);


				imageDimension = map.GetOutputSizes((int)ImageFormatType.Jpeg)[0];
				// Add permission for camera and let user grant the permission
				if (CheckSelfPermission(Manifest.Permission.Camera) != Android.Content.PM.Permission.Granted && CheckSelfPermission(Manifest.Permission.WriteExternalStorage) != Android.Content.PM.Permission.Granted)
				{
					RequestPermissions(new string[] { Manifest.Permission.Camera, Manifest.Permission.WriteExternalStorage }, REQUEST_CAMERA_PERMISSION);
					return;
				}
				manager.OpenCamera(cameraId, new StateCallback(this), null);
			}

			catch (CameraAccessException e)
			{

			}

		}

		public void CreateCameraPreview()
		{
			try
			{
				SurfaceTexture texture = textureView.SurfaceTexture;

				texture.SetDefaultBufferSize(imageDimension.Width, imageDimension.Height);
				Surface surface = new Surface(texture);
				captureRequestBuilder = cameraDevice.CreateCaptureRequest(CameraTemplate.Preview);
				captureRequestBuilder.AddTarget(surface);
				List<Surface> surfacelst = new List<Surface>();
				surfacelst.Add(surface);
				cameraDevice.CreateCaptureSession(surfacelst, new CaptureCallback(this, false), null);

			}
			catch (CameraAccessException e)
			{

			}
		}
		public void UpdatePreview()
		{
			try
			{
				if (null == cameraDevice)
				{
					return;
				}
				captureRequestBuilder.Set(CaptureRequest.ControlMode, (int)ControlMode.Auto);
				cameraCaptureSessions.SetRepeatingRequest(captureRequestBuilder.Build(), null, null);
			}
			catch (System.Exception e)
			{

			}
		}
	}


	public class CaptureCallback : CameraCaptureSession.StateCallback
	{
		MainActivity actvty;
		bool blnCaptureSession;
		public CaptureCallback(MainActivity activity, bool blnIsCapture)
		{
			actvty = activity;
			blnCaptureSession = blnIsCapture;
		}
		public override void OnConfigured(CameraCaptureSession session)
		{
			if (null == actvty.cameraDevice)
			{
				return;
			}
			if (blnCaptureSession)
			{
				session.Capture(actvty.captureBuilder.Build(), new CaptureSessionCallback(actvty), actvty.mBackgroundHandler);
			}
			else
			{
				actvty.cameraCaptureSessions = session;
				actvty.UpdatePreview();
			}
		}

		public override void OnConfigureFailed(CameraCaptureSession session)
		{
			//throw new NotImplementedException();
		}
	}



	public class StateCallback : CameraDevice.StateCallback
	{
		MainActivity actvty;
		public StateCallback(MainActivity activity)
		{
			actvty = activity;
		}
		public override void OnDisconnected(CameraDevice camera)
		{
			actvty.cameraDevice.Close();
			actvty.cameraDevice = null;
		}

		public override void OnError(CameraDevice camera, [GeneratedEnum] CameraError error)
		{
			//throw new NotImplementedException();
		}

		public override void OnOpened(CameraDevice camera)
		{
			actvty.cameraDevice = camera;
			actvty.CreateCameraPreview();
		}
	}



	//readonly ImageReader.IOnImageAvailableListener mOnJpegImageAvailableListener = new SavePhoto();
	class SavePhoto : Java.Lang.Object, ImageReader.IOnImageAvailableListener
	{
		File file;
		MainActivity mainActivity;
		ImageReader imgreader;
		public SavePhoto(MainActivity activity)
		{
			mainActivity = activity;
		}
		public void OnImageAvailable(ImageReader reader)
		{
			System.Console.WriteLine("On Image Available - Called");

			Image image = null;
			try
			{
				imgreader = reader;
				image = reader.AcquireLatestImage();
				ByteBuffer buffer = image.GetPlanes()[0].Buffer;
				byte[] bytes = new byte[buffer.Capacity()];
				buffer.Get(bytes);
				System.Console.WriteLine("On Image Available - Before Saving");

				save(bytes);
			}
			catch (System.Exception ex)
			{
				System.Console.WriteLine(ex.ToString());

				System.Console.WriteLine("On Image Available - Exception Occurred");

			}
		}
		private void save(byte[] bytes)
		{

			//Toast.MakeText(mainActivity.ApplicationContext, mainActivity.GetExternalFilesDir(Android.OS.Environment.DirectoryDcim) + "/" + DateTime.Now + ".jpg", ToastLength.Long).Show();
			file = new File(mainActivity.GetExternalFilesDir(Android.OS.Environment.DirectoryDcim) + "/" + DateTime.Now.ToString("MMMMddyyyyhmmsstt") + ".jpg");
			ContentValues values = new ContentValues();
			values.Put("_data", file.AbsolutePath);
			ContentResolver cr = mainActivity.ContentResolver;
			cr.Insert(MediaStore.Images.Media.ExternalContentUri, values);
			//mainActivity.mBackgroundHandler.Post(imgreader.AcquireNextImage(), file);

			OutputStream output = null;
			try
			{
				output = new FileOutputStream(file);
				output.Write(bytes);
			}
			finally
			{
				if (null != output)
				{
					output.Close();
				}
			}
			//TODO : Need to write the code for trimming the image according to the padding.

			System.Console.WriteLine("Successfully written to " + mainActivity.GetExternalFilesDir(Android.OS.Environment.DirectoryDcim) + "/test.jpg");
		}
	}


	public class CaptureSessionCallback : CameraCaptureSession.CaptureCallback
	{
		MainActivity actvty;
		public CaptureSessionCallback(MainActivity activity)
		{
			actvty = activity;
		}

		public override void OnCaptureCompleted(CameraCaptureSession session, CaptureRequest request, TotalCaptureResult result)
		{
			base.OnCaptureCompleted(session, request, result);
			actvty.CreateCameraPreview();

			//showCapturedImage(file);
		}
	}












}






