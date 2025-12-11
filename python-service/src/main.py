import cv2
import asyncio
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
    
    def open_camera(self):
        self.cap = cv2.VideoCapture(0)
        self.cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
        self.cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
    
    async def process_frame(self):
        """Process one frame and analyze posture."""
        ret, frame = self.cap.read()
        if not ret:
            return None
        
        landmarks = self.detector.detect(frame)
        if landmarks:
            result = self.analyzer.analyze(landmarks.landmark)
            return result
        return None
    
    async def run(self):
        """Main loop: capture, analyze, send."""
        self.running = True
        self.open_camera()
        
        # Start WebSocket server
        server_task = asyncio.create_task(self.ws_server.start())
        await asyncio.sleep(1)  # Let server start
        
        print("Posture tracking started...")
        
        try:
            while self.running:
                result = await self.process_frame()
                if result:
                    await self.ws_server.send(result)
                await asyncio.sleep(0.1)  # 10 FPS
        finally:
            self.cap.release()
            self.detector.close()
            server_task.cancel()

if __name__ == "__main__":
    service = PostureService()
    try:
        asyncio.run(service.run())
    except KeyboardInterrupt:
        print("\nStopping...")
        service.running = False
