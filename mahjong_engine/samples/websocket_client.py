import asyncio
import json
import aioconsole
import websockets

async def client(uri):
    async with websockets.connect(uri) as websocket:
        await websocket.send("Hello, Server!")

        async def send_messages():
            while True:
                user_input = await aioconsole.ainput("Enter message to send (or 'exit' to quit): \n")
                if user_input.lower() == 'exit':
                    print("Exiting...")
                    break
                await websocket.send(user_input)

        async def receive_messages():
            while True:
                try:
                    message = await websocket.recv()
                    print(f"Received from server: {message}")
                except websockets.ConnectionClosed:
                    print("Connection closed by server")
                    break


        await asyncio.gather(send_messages(), receive_messages())

asyncio.run(client("ws://localhost:8765"))