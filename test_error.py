import asyncio
import json
import traceback
import websockets

async def main():
    try:
        uri = "ws://127.0.0.1:8765"
        async with websockets.connect(uri) as ws:
            # 1. Wait for connected
            connected_msg = await ws.recv()
            print("Connected:", connected_msg)
            
            # 2. Join
            await ws.send(json.dumps({"type": "join"}))
            
            # 3. Open another connection to trigger match
            async with websockets.connect(uri) as ws2:
                await ws2.recv()
                await ws2.send(json.dumps({"type": "join"}))
                
                # 4. Wait for match to start
                while True:
                    msg = json.loads(await ws.recv())
                    print("ws1:", msg["type"])
                    if msg["type"] == "wall_dealt":
                        my_hand_data = None
                        for h in msg["hands"]:
                            # Assume ws1 is C0001
                            if "tenpai_examples" in h and h["tenpai_examples"]:
                                my_hand_data = h
                                break
                        
                        if my_hand_data and len(my_hand_data["tenpai_examples"]) > 0:
                            # Send action "selected" with bad data
                            # We send the first tenpai example from the server to simulate normal play,
                            # but let's just see if it crashes.
                            
                            sample_hand = my_hand_data["tenpai_examples"][0]
                            # Let's add a fake tile
                            # sample_hand.append(22)  # NO, let's just send the legitimate tenpai example. The client is doing exactly this:
                            
                            payload = {
                                "type": "action",
                                "action": "selected",
                                "data": {
                                    "hand": sample_hand
                                }
                            }
                            print("Sending:", payload)
                            await ws.send(json.dumps(payload))
                            
                            resp = await ws.recv()
                            print("Response to selected:", resp)
                            break
    except Exception as e:
        traceback.print_exc()

asyncio.run(main())
