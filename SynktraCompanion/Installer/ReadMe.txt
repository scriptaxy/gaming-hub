Synktra Companion - Desktop Gaming Hub Controller
==================================================

Thank you for installing Synktra Companion!

WHAT IS SYNKTRA COMPANION?
--------------------------
Synktra Companion is a Windows desktop application that works with the 
Synktra iOS app to enable:

  * Remote game launching from your iPhone
  * PC status monitoring (CPU, GPU, Memory)
  * Game streaming to your mobile device
  * Virtual Xbox 360 controller support

SYSTEM REQUIREMENTS
-------------------
  * Windows 10/11 (64-bit)
  * 4 GB RAM minimum
  * DirectX 11 compatible graphics card
  * Network connection (same WiFi as your iPhone)

OPTIONAL: VIRTUAL CONTROLLER
----------------------------
For the best gaming experience, we recommend installing the ViGEmBus driver.
This creates a virtual Xbox 360 controller that games recognize as a real
gamepad connected to your PC.

Benefits:
  * Games with controller support work automatically
  * Full analog stick and trigger support
  * Proper button mapping for all games

If you choose not to install ViGEmBus, the app will fall back to 
keyboard/mouse emulation mode.

FIREWALL CONFIGURATION
----------------------
The installer can automatically configure Windows Firewall to allow:
  * Port 19500 (TCP) - API Server
  * Port 19501 (TCP) - WebSocket Streaming
  * Port 19502 (UDP) - Low-latency Streaming
  * Port 5001 (UDP) - Device Discovery

If you skip this step, you may need to manually allow these ports or
the app may not be discoverable from your iPhone.

GETTING STARTED
---------------
1. Launch Synktra Companion after installation
2. The app will minimize to the system tray
3. Open the Synktra app on your iPhone
4. Go to Remote PC and scan for your computer
5. Connect and start gaming!

SUPPORT
-------
GitHub: https://github.com/scriptaxy/gaming-hub
Issues: https://github.com/scriptaxy/gaming-hub/issues

© 2025 Scriptaxy. All rights reserved.
