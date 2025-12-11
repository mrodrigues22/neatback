import cv2
import asyncio
import base64
from pose_detector import PoseDetector
from posture_analyzer import PostureAnalyzer
from websocket_server import WebSocketServer

class PostureService:
    def __init__(self):
        self.detector = PoseDetector()
        self.analyzer = PostureAnalyzer()
        self.ws_server = WebSocketServer()
        self.cap = None
        self.running = False
        self.monitoring = False  # Track if we should be monitoring
    
    def open_camera(self):
        if self.cap is None:
            print("Opening camera...")
            self.cap = cv2.VideoCapture(0)
            self.cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
            self.cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
    
    def close_camera(self):
        if self.cap is not None:
            print("Closing camera...")
            self.cap.release()
            self.cap = None
    
    async def process_frame(self):
        """Process one frame and analyze posture."""
        if self.cap is None:
            return None
            
        ret, frame = self.cap.read()
        if not ret:
            return None
        
        landmarks = self.detector.detect(frame)
        if landmarks:
            result = self.analyzer.analyze(landmarks.landmark)
            
            # Encode frame as JPEG and convert to base64
            _, buffer = cv2.imencode('.jpg', frame, [cv2.IMWRITE_JPEG_QUALITY, 80])
            frame_base64 = base64.b64encode(buffer).decode('utf-8')
            result['frame'] = frame_base64
            
            return result
        return None
    
    async def on_client_change(self, has_clients):
        """Called when client connection state changes."""
        self.monitoring = has_clients
        if has_clients:
            self.open_camera()
        else:
            self.close_camera()
    
    async def run(self):
        """Main loop: capture, analyze, send."""
        self.running = True
        
        # Register callback for client state changes
        self.ws_server.on_client_change = self.on_client_change
        
        # Start WebSocket server
        server_task = asyncio.create_task(self.ws_server.start())
        await asyncio.sleep(1)  # Let server start
        
        print("Posture tracking service ready. Waiting for client connection...")
        
        try:
            while self.running:
                if self.monitoring:
                    result = await self.process_frame()
                    if result:
                        await self.ws_server.send(result)
                    await asyncio.sleep(0.1)  # 10 FPS
                else:
                    await asyncio.sleep(0.5)  # Check less frequently when not monitoring
        finally:
            self.close_camera()
            self.detector.close()
            server_task.cancel()

if __name__ == "__main__":
    service = PostureService()
    try:
        asyncio.run(service.run())
    except KeyboardInterrupt:
        print("\nStopping...")
        service.running = False
