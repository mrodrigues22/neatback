import asyncio
import websockets
import json
import cv2
import base64
import numpy as np
import time

class WebSocketServer:
    def __init__(self, host='localhost', port=8765):
        self.host = host
        self.port = port
        self.clients = set()
        self.on_client_change = None  # Callback for when clients connect/disconnect
        self.detector = None  # Will be set externally
        self.analyzer = None  # Will be set externally
        self.camera = None
        self.is_monitoring = False
        self.monitoring_task = None
        
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
            
            if msg_type == 'start_monitoring':
                # Start camera and monitoring
                await self.start_monitoring()
            
            elif msg_type == 'stop_monitoring':
                # Stop monitoring
                await self.stop_monitoring()
            
            elif msg_type == 'save_good_posture':
                # Save baseline posture with current frame
                await self.handle_save_current_posture(websocket)
            
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
    
    async def start_monitoring(self):
        """Start camera and monitoring loop."""
        if self.is_monitoring:
            return
        
        print("Starting camera...")
        self.camera = cv2.VideoCapture(0)
        
        # Set camera properties for better performance
        self.camera.set(cv2.CAP_PROP_FRAME_WIDTH, 1280)
        self.camera.set(cv2.CAP_PROP_FRAME_HEIGHT, 720)
        self.camera.set(cv2.CAP_PROP_FPS, 30)
        self.camera.set(cv2.CAP_PROP_BUFFERSIZE, 1)  # Reduce buffer to get latest frames
        
        if not self.camera.isOpened():
            await self.send({
                'type': 'error',
                'message': 'Failed to open camera'
            })
            return
        
        self.is_monitoring = True
        self.monitoring_task = asyncio.create_task(self.monitoring_loop())
        
        await self.send({
            'type': 'monitoring_started',
            'success': True
        })
        print("Monitoring started")
    
    async def stop_monitoring(self):
        """Stop camera and monitoring loop."""
        if not self.is_monitoring:
            return
        
        self.is_monitoring = False
        
        if self.monitoring_task:
            self.monitoring_task.cancel()
            try:
                await self.monitoring_task
            except asyncio.CancelledError:
                pass
        
        if self.camera:
            self.camera.release()
            self.camera = None
        
        await self.send({
            'type': 'monitoring_stopped',
            'success': True
        })
        print("Monitoring stopped")
    
    async def monitoring_loop(self):
        """Continuously capture and analyze frames."""
        try:
            while self.is_monitoring:
                if not self.camera or not self.camera.isOpened():
                    break
                
                ret, frame = self.camera.read()
                if not ret:
                    print("Failed to read frame")
                    await asyncio.sleep(0.1)
                    continue
                
                # Get timestamp
                timestamp_ms = int(time.time() * 1000)
                
                # Analyze posture
                posture_status = self.detector.check_posture(frame, timestamp_ms)
                
                # Update analyzer
                analysis = self.analyzer.update(posture_status)
                
                # Resize frame for preview (reduces data transfer significantly)
                preview_frame = cv2.resize(frame, (640, 360))
                
                # Encode frame to JPEG with lower quality for preview
                _, buffer = cv2.imencode('.jpg', preview_frame, [cv2.IMWRITE_JPEG_QUALITY, 50])
                frame_base64 = base64.b64encode(buffer).decode('utf-8')
                
                # Send results to all clients
                await self.send({
                    'type': 'posture_result',
                    'data': {
                        'is_bad': posture_status['is_bad'],
                        'pitch_angle': posture_status['pitch_angle'],
                        'adjusted_pitch': posture_status['adjusted_pitch'],
                        'distance': posture_status['distance'],
                        'bad_duration': analysis['bad_duration'],
                        'should_warn': analysis['should_warn'],
                        'message': analysis['message'],
                        'error': posture_status.get('error'),
                        'frame': frame_base64
                    }
                })
                
                # Process at ~10 FPS for smoother preview
                await asyncio.sleep(0.1)
        
        except asyncio.CancelledError:
            print("Monitoring loop cancelled")
        except Exception as e:
            print(f"Error in monitoring loop: {e}")
            await self.send({
                'type': 'error',
                'message': f'Monitoring error: {str(e)}'
            })
    
    async def handle_save_current_posture(self, websocket):
        """Save good posture baseline from current camera frame."""
        if not self.detector:
            await websocket.send(json.dumps({
                'type': 'error',
                'message': 'Detector not initialized'
            }))
            return
        
        if not self.camera or not self.camera.isOpened():
            await websocket.send(json.dumps({
                'type': 'error',
                'message': 'Camera not active'
            }))
            return
        
        try:
            # Capture current frame
            ret, frame = self.camera.read()
            if not ret:
                await websocket.send(json.dumps({
                    'type': 'error',
                    'message': 'Failed to capture frame'
                }))
                return
            
            # Get timestamp
            timestamp_ms = int(time.time() * 1000)
            
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
