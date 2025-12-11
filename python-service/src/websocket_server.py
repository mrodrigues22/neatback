import asyncio
import websockets
import json

class WebSocketServer:
    def __init__(self, host='localhost', port=8765):
        self.host = host
        self.port = port
        self.clients = set()
        self.on_client_change = None  # Callback for when clients connect/disconnect
        
    async def register(self, websocket):
        self.clients.add(websocket)
        print(f"Client connected")
        if self.on_client_change:
            await self.on_client_change(True)
        
    async def unregister(self, websocket):
        self.clients.remove(websocket)
        print(f"Client disconnected")
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
            await websocket.wait_closed()
        finally:
            await self.unregister(websocket)
    
    async def start(self):
        print(f"WebSocket server starting on ws://{self.host}:{self.port}")
        async with websockets.serve(self.handler, self.host, self.port):
            await asyncio.Future()
