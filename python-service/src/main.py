import asyncio
import sys
import os
import traceback
from pose_detector import PostureDetector
from posture_analyzer import PostureAnalyzer
from websocket_server import WebSocketServer

class PostureService:
    def __init__(self):
        print("Initializing Slouti Posture Service...", flush=True)
        
        # Print diagnostic info
        if getattr(sys, 'frozen', False):
            print(f"Running as executable from: {sys._MEIPASS}", flush=True)
            print(f"Executable path: {sys.executable}", flush=True)
        else:
            print(f"Running as script from: {os.path.dirname(__file__)}", flush=True)
        
        try:
            print("Initializing PostureDetector...", flush=True)
            self.detector = PostureDetector()
            print("PostureDetector initialized successfully", flush=True)
            
            print("Initializing PostureAnalyzer...", flush=True)
            self.analyzer = PostureAnalyzer()
            print("PostureAnalyzer initialized successfully", flush=True)
            
            print("Initializing WebSocketServer...", flush=True)
            self.ws_server = WebSocketServer()
            print("WebSocketServer initialized successfully", flush=True)
            
            # Link detector and analyzer to WebSocket server
            self.ws_server.detector = self.detector
            self.ws_server.analyzer = self.analyzer
            
            self.running = False
            print("Service initialization complete", flush=True)
        except Exception as e:
            print(f"ERROR during initialization: {e}", file=sys.stderr, flush=True)
            print(f"Traceback: {traceback.format_exc()}", file=sys.stderr, flush=True)
            raise
    
    async def run(self):
        """Run the WebSocket server and wait for client connections."""
        self.running = True
        
        try:
            print("Starting WebSocket server on ws://localhost:8765", flush=True)
            # Start WebSocket server (runs indefinitely)
            await self.ws_server.start()
        except KeyboardInterrupt:
            print("Service interrupted by user", flush=True)
        except Exception as e:
            print(f"ERROR during service runtime: {e}", file=sys.stderr, flush=True)
            print(f"Traceback: {traceback.format_exc()}", file=sys.stderr, flush=True)
            raise
        finally:
            self.running = False
            self.detector.close()
            print("Service shutdown complete", flush=True)

if __name__ == "__main__":
    try:
        service = PostureService()
        asyncio.run(service.run())
    except KeyboardInterrupt:
        print("Service stopped by user", flush=True)
    except Exception as e:
        print(f"FATAL ERROR: {e}", file=sys.stderr, flush=True)
        print(f"Traceback: {traceback.format_exc()}", file=sys.stderr, flush=True)
        sys.exit(1)
