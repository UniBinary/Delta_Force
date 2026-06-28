from pynput.mouse import Listener, Button

points = []

def on_click(x, y, button, pressed):
    if not pressed:
        return
    if button == Button.left:
        points.append((x, y))
        print(f"📍 第{len(points)}个点 = ({x}, {y})")
        if len(points) == 2:
            print(f"\n✅ 你的 bbox = {(*points[0], *points[1])}")
    elif button == Button.right:
        print("(右键退出)")
        return False

print("左键 = 标记坐标，右键 = 退出")
with Listener(on_click=on_click) as l:
    l.join()