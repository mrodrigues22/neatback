import asyncio
from pose_detector import PostureDetector
from posture_analyzer import PostureAnalyzer
from websocket_server import WebSocketServer

class PostureService:
    def __init__(self):
        self.detector = PostureDetector()
        self.analyzer = PostureAnalyzer()
        self.ws_server = WebSocketServer()
        
        # Link detector and analyzer to WebSocket server
        self.ws_server.detector = self.detector
        self.ws_server.analyzer = self.analyzer
        
        self.running = False
    
    async def run(self):
        """Run the WebSocket server and wait for client connections."""
        self.running = True
        
        print("="*60)
        print("NeatBack Posture Analysis Service")
        print("="*60)
        print("Starting WebSocket server...")
        print("\nWaiting for client connection...")
        print("="*60)
        
        try:
            # Start WebSocket server (runs indefinitely)
            await self.ws_server.start()
        except KeyboardInterrupt:
            print("\nShutting down...")
        finally:
            self.running = False
            self.detector.close()
            print("Service stopped.")

if __name__ == "__main__":
    service = PostureService()
    try:
        asyncio.run(service.run())
    except KeyboardInterrupt:
        print("\nStopping...")
        service.running = False
