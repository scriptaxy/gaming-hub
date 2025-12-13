using CoreMotion;
using Foundation;

namespace gaming_hub.Services
{
    /// <summary>
    /// Service for gyroscope/motion-based aiming controls
    /// </summary>
    public class GyroscopeAimingService : IDisposable
    {
        private static GyroscopeAimingService? _instance;
        public static GyroscopeAimingService Instance => _instance ??= new GyroscopeAimingService();

        private CMMotionManager _motionManager;
        private NSOperationQueue _operationQueue;
        private bool _isRunning;
        
        // Calibration offset
        private double _pitchOffset;
        private double _yawOffset;
        
    // Settings
        public float Sensitivity { get; set; } = 1.0f;
        public bool InvertY { get; set; } = false;
        public bool InvertX { get; set; } = false;
        public float DeadZone { get; set; } = 0.02f;
        
   // Current values (normalized -1 to 1)
        public float CurrentX { get; private set; }
        public float CurrentY { get; private set; }

        public event Action<float, float>? OnMotionUpdate;
        public event Action? OnCalibrationComplete;

        public bool IsAvailable => _motionManager.DeviceMotionAvailable;
        public bool IsRunning => _isRunning;

        private GyroscopeAimingService()
        {
            _motionManager = new CMMotionManager();
            _operationQueue = new NSOperationQueue();
     
    // Higher update rate for responsive aiming
            _motionManager.DeviceMotionUpdateInterval = 1.0 / 120.0; // 120Hz
}

  /// <summary>
        /// Start motion tracking for gyro aiming
  /// </summary>
        public void StartTracking()
   {
          if (_isRunning || !IsAvailable) return;
  _isRunning = true;

            _motionManager.StartDeviceMotionUpdates(
CMAttitudeReferenceFrame.XArbitraryZVertical,
   _operationQueue,
    (motion, error) =>
    {
     if (error != null || motion == null) return;
        ProcessMotion(motion);
  });
  }

    /// <summary>
 /// Stop motion tracking
        /// </summary>
        public void StopTracking()
      {
         if (!_isRunning) return;
            _isRunning = false;
          _motionManager.StopDeviceMotionUpdates();
        }

        /// <summary>
        /// Calibrate the current position as center (neutral)
        /// </summary>
        public void Calibrate()
        {
   var motion = _motionManager.DeviceMotion;
        if (motion == null) return;

            _pitchOffset = motion.Attitude.Pitch;
   _yawOffset = motion.Attitude.Yaw;
  
            CurrentX = 0;
            CurrentY = 0;
  
            OnCalibrationComplete?.Invoke();
     }

        /// <summary>
        /// Reset calibration to default
        /// </summary>
    public void ResetCalibration()
        {
            _pitchOffset = 0;
            _yawOffset = 0;
        }

        private void ProcessMotion(CMDeviceMotion motion)
        {
 // Get rotation rates (radians per second)
        var rotationRate = motion.RotationRate;
     
            // Calculate delta from rotation rate
            // This gives us incremental movement rather than absolute position
            var deltaX = (float)(rotationRate.y * Sensitivity * 0.02);
          var deltaY = (float)(rotationRate.x * Sensitivity * 0.02);
     
   // Apply dead zone
       if (Math.Abs(deltaX) < DeadZone) deltaX = 0;
          if (Math.Abs(deltaY) < DeadZone) deltaY = 0;
          
     // Apply inversion
   if (InvertX) deltaX = -deltaX;
  if (InvertY) deltaY = -deltaY;

  // Clamp values
            CurrentX = Math.Clamp(CurrentX + deltaX, -1f, 1f);
            CurrentY = Math.Clamp(CurrentY + deltaY, -1f, 1f);

 // Only fire event if there's meaningful input
          if (Math.Abs(deltaX) > 0.001f || Math.Abs(deltaY) > 0.001f)
            {
  OnMotionUpdate?.Invoke(deltaX, deltaY);
    }
        }

        /// <summary>
  /// Get absolute position mode (alternative to incremental)
   /// Useful for some game types
        /// </summary>
        public (float x, float y) GetAbsolutePosition()
  {
       var motion = _motionManager.DeviceMotion;
         if (motion == null) return (0, 0);

   var attitude = motion.Attitude;
            
     // Calculate position relative to calibration point
            var pitch = attitude.Pitch - _pitchOffset;
            var yaw = attitude.Yaw - _yawOffset;
   
         // Normalize to -1 to 1 range (assuming ~60 degree range of motion)
            var maxAngle = Math.PI / 3; // 60 degrees
            var x = (float)Math.Clamp(yaw / maxAngle, -1, 1);
var y = (float)Math.Clamp(pitch / maxAngle, -1, 1);
            
  if (InvertX) x = -x;
     if (InvertY) y = -y;
            
       return (x * Sensitivity, y * Sensitivity);
   }

    public void Dispose()
     {
     StopTracking();
            _motionManager.Dispose();
            _operationQueue.Dispose();
 _instance = null;
        }
    }
}
