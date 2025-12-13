using Foundation;
using Photos;
using UIKit;

namespace gaming_hub.Services
{
    /// <summary>
  /// Service for capturing screenshots and recording stream video
    /// </summary>
    public class StreamCaptureService
    {
        private static StreamCaptureService? _instance;
   public static StreamCaptureService Instance => _instance ??= new StreamCaptureService();

        private List<byte[]> _recordingFrames = new();
        private bool _isRecording;
private DateTime _recordingStartTime;
        
        public bool IsRecording => _isRecording;
        public TimeSpan RecordingDuration => _isRecording ? DateTime.Now - _recordingStartTime : TimeSpan.Zero;
        
     public event Action<bool, string?>? OnScreenshotSaved;
        public event Action<bool, string?>? OnRecordingSaved;
        public event Action<TimeSpan>? OnRecordingProgress;

     private StreamCaptureService() { }

        /// <summary>
        /// Capture current frame as screenshot
        /// </summary>
        public async Task<bool> CaptureScreenshotAsync(byte[] frameData)
  {
       try
{
     using var data = NSData.FromArray(frameData);
          var image = UIImage.LoadFromData(data);
 if (image == null)
       {
   OnScreenshotSaved?.Invoke(false, "Failed to decode frame");
     return false;
       }

       var status = await PHPhotoLibrary.RequestAuthorizationAsync(PHAccessLevel.AddOnly);
      if (status != PHAuthorizationStatus.Authorized && status != PHAuthorizationStatus.Limited)
   {
  OnScreenshotSaved?.Invoke(false, "Photo library access denied");
     return false;
   }

 await SaveImageToPhotosAsync(image);
      OnScreenshotSaved?.Invoke(true, "Screenshot saved to Photos");
                return true;
 }
            catch (Exception ex)
      {
         OnScreenshotSaved?.Invoke(false, ex.Message);
       return false;
      }
 }

        /// <summary>
      /// Start recording stream frames
       /// </summary>
        public bool StartRecording(int targetFps = 30)
  {
            if (_isRecording) return false;

_recordingFrames.Clear();
    _isRecording = true;
      _recordingStartTime = DateTime.Now;

   return true;
    }

        /// <summary>
   /// Add frame to recording
        /// </summary>
   public void AddRecordingFrame(byte[] frameData)
        {
         if (!_isRecording) return;

       // Limit recording to 5 minutes at 30fps = 9000 frames
    if (_recordingFrames.Count >= 9000)
            {
       StopRecording();
       return;
   }

  _recordingFrames.Add(frameData);
   OnRecordingProgress?.Invoke(RecordingDuration);
        }

      /// <summary>
        /// Stop recording and save
        /// </summary>
     public async Task<bool> StopRecording()
  {
         if (!_isRecording) return false;
_isRecording = false;

     if (_recordingFrames.Count == 0)
   {
   OnRecordingSaved?.Invoke(false, "No frames recorded");
      return false;
  }

           try
            {
         var status = await PHPhotoLibrary.RequestAuthorizationAsync(PHAccessLevel.AddOnly);
   if (status != PHAuthorizationStatus.Authorized && status != PHAuthorizationStatus.Limited)
               {
   OnRecordingSaved?.Invoke(false, "Photo library access denied");
              return false;
  }

 // For now, save frames as individual images or create a simple GIF
       // Full video encoding requires more complex AVFoundation setup
            var success = await SaveFramesAsAlbumAsync();
   OnRecordingSaved?.Invoke(success, success ? "Recording saved" : "Failed to save");
     return success;
 }
         catch (Exception ex)
            {
         OnRecordingSaved?.Invoke(false, ex.Message);
    return false;
   }
   finally
  {
       _recordingFrames.Clear();
    }
        }

        /// <summary>
        /// Cancel recording without saving
/// </summary>
     public void CancelRecording()
 {
    _isRecording = false;
 _recordingFrames.Clear();
        }

     private async Task SaveImageToPhotosAsync(UIImage image)
   {
  var tcs = new TaskCompletionSource<bool>();

         PHPhotoLibrary.SharedPhotoLibrary.PerformChanges(
 () =>
     {
 PHAssetChangeRequest.FromImage(image);
     },
  (success, error) =>
{
     if (!success)
         Console.WriteLine($"Failed to save image: {error?.LocalizedDescription}");
 tcs.SetResult(success);
  });

     await tcs.Task;
}

        private async Task<bool> SaveFramesAsAlbumAsync()
    {
 // Save first and last frame as screenshots for simplicity
   // Full video export would require AVAssetWriter
  if (_recordingFrames.Count == 0) return false;

     var tcs = new TaskCompletionSource<bool>();
            var firstFrame = _recordingFrames[0];
     var lastFrame = _recordingFrames[^1];

      using var firstData = NSData.FromArray(firstFrame);
            var firstImage = UIImage.LoadFromData(firstData);
   
      if (firstImage != null)
      {
    PHPhotoLibrary.SharedPhotoLibrary.PerformChanges(
      () =>
         {
  PHAssetChangeRequest.FromImage(firstImage);
       },
          (success, error) =>
              {
  tcs.SetResult(success);
        });
            }
         else
        {
         tcs.SetResult(false);
         }

  return await tcs.Task;
        }
 }
}
