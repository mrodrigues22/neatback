import asyncio
import websockets
import json

class WebSocketServer:
    def __init__(self, host='localhost', port=8765):
        self.host = host
        self.port = port
        self.clients = set()
        
    async def register(self, websocket):
        self.clients.add(websocket)
        print(f"Client connected")
        
    async def unregister(self, websocket):
        self.clients.remove(websocket)
        print(f"Client disconnected")
        
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
            await websocket.wait_closed()
        finally:
            await self.unregister(websocket)
    
    async def start(self):
        print(f"WebSocket server starting on ws://{self.host}:{self.port}")
        async with websockets.serve(self.handler, self.host, self.port):
            await asyncio.Future()
