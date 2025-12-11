import asyncio
import websockets
import json
import cv2
import base64
import numpy as np

class WebSocketServer:
    def __init__(self, host='localhost', port=8765):
        self.host = host
        self.port = port
        self.clients = set()
        self.on_client_change = None  # Callback for when clients connect/disconnect
        self.detector = None  # Will be set externally
        self.analyzer = None  # Will be set externally
        
    async def register(self, websocket):
        self.clients.add(websocket)
        print(f"Client connected. Total clients: {len(self.clients)}")
        if self.on_client_change:
            await self.on_client_change(True)
        
    async def unregister(self, websocket):
        self.clients.remove(websocket)
        print(f"Client disconnected. Total clients: {len(self.clients)}")
        if self.on_client_change:
            await self.on_client_change(len(self.clients) > 0)
        
    async def send(self, data):
        """Send data to all connected clients."""
        if not self.clients:
            return
        message = json.dumps(data)
        await asyncio.gather(
            *[client.send(message) for client in self.clients],
            return_exceptions=True
        )
    
    async def handler(self, websocket):
        await self.register(websocket)
        try:
            async for message in websocket:
                await self.process_message(websocket, message)
        except websockets.exceptions.ConnectionClosed:
            print("Client connection closed")
        finally:
            await self.unregister(websocket)
    
    async def process_message(self, websocket, message):
        """Process incoming message from client."""
        try:
            data = json.loads(message)
            msg_type = data.get('type')
            
            if msg_type == 'frame':
                # Process video frame
                await self.handle_frame(websocket, data)
            
            elif msg_type == 'save_good_posture':
                # Save baseline posture
                await self.handle_save_posture(websocket, data)
            
            elif msg_type == 'get_statistics':
                # Return statistics
                if self.analyzer:
                    stats = self.analyzer.get_statistics()
                    await websocket.send(json.dumps({
                        'type': 'statistics',
                        'data': stats
                    }))
            
            elif msg_type == 'reset_statistics':
                # Reset statistics
                if self.analyzer:
                    self.analyzer.reset_statistics()
                    await websocket.send(json.dumps({
                        'type': 'statistics_reset',
                        'success': True
                    }))
            
            elif msg_type == 'set_thresholds':
                # Update thresholds
                if self.detector:
                    self.detector.pitch_threshold = data.get('pitch_threshold', -10)
                    self.detector.distance_threshold = data.get('distance_threshold', 10)
                    await websocket.send(json.dumps({
                        'type': 'thresholds_updated',
                        'success': True
                    }))
        
        except Exception as e:
            print(f"Error processing message: {e}")
            await websocket.send(json.dumps({
                'type': 'error',
                'message': str(e)
            }))
    
    async def handle_frame(self, websocket, data):
        """Process video frame and return posture analysis."""
        if not self.detector or not self.analyzer:
            await websocket.send(json.dumps({
                'type': 'error',
                'message': 'Detector or analyzer not initialized'
            }))
            return
        
        try:
            # Decode base64 image
            img_data = base64.b64decode(data['frame'])
            nparr = np.frombuffer(img_data, np.uint8)
            frame = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
            
            # Get timestamp (or use current time)
            timestamp_ms = data.get('timestamp_ms', int(cv2.getTickCount() / cv2.getTickFrequency() * 1000))
            
            # Analyze posture
            posture_status = self.detector.check_posture(frame, timestamp_ms)
            
            # Update analyzer
            analysis = self.analyzer.update(posture_status)
            
            # Send response
            await websocket.send(json.dumps({
                'type': 'posture_result',
                'data': {
                    'is_bad': posture_status['is_bad'],
                    'pitch_angle': posture_status['pitch_angle'],
                    'adjusted_pitch': posture_status['adjusted_pitch'],
                    'distance': posture_status['distance'],
                    'bad_duration': analysis['bad_duration'],
                    'should_warn': analysis['should_warn'],
                    'message': analysis['message'],
                    'error': posture_status.get('error')
                }
            }))
        except Exception as e:
            print(f"Error handling frame: {e}")
            await websocket.send(json.dumps({
                'type': 'error',
                'message': f'Frame processing error: {str(e)}'
            }))
    
    async def handle_save_posture(self, websocket, data):
        """Save good posture baseline."""
        if not self.detector:
            await websocket.send(json.dumps({
                'type': 'error',
                'message': 'Detector not initialized'
            }))
            return
        
        try:
            # Decode image
            img_data = base64.b64decode(data['frame'])
            nparr = np.frombuffer(img_data, np.uint8)
            frame = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
            
            # Get timestamp
            timestamp_ms = data.get('timestamp_ms', int(cv2.getTickCount() / cv2.getTickFrequency() * 1000))
            
            # Save baseline
            success = self.detector.save_good_posture(frame, timestamp_ms)
            
            await websocket.send(json.dumps({
                'type': 'posture_saved',
                'success': success,
                'good_pitch': self.detector.good_head_pitch_angle,
                'good_distance': self.detector.good_head_distance
            }))
        except Exception as e:
            print(f"Error saving posture: {e}")
            await websocket.send(json.dumps({
                'type': 'error',
                'message': f'Failed to save posture: {str(e)}'
            }))
    
    async def start(self):
        print(f"WebSocket server starting on ws://{self.host}:{self.port}")
        async with websockets.serve(self.handler, self.host, self.port):
            await asyncio.Future()
