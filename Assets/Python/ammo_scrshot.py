import os
import sys
from PIL import ImageGrab
from time import sleep

def capture_ammo_sprites():
    # 1. 获取用户输入的前缀名
    prefix = input("请输入本次截图的前缀名（例如 556x45_RNIP）: ").strip()
    if not prefix:
        print("❌ 错误：前缀名不能为空。")
        sys.exit(1)

    print("请在3秒内切换到目标窗口，准备好截图。")
    sleep(3)  # 给用户3秒钟时间切换到目标窗口

    # 2. 定义你提供的六个格子坐标 (bbox)
    bboxes = [
        (2773, 730, 2854, 815),
        (2772, 816, 2858, 898),
        (2772, 901, 2858, 984),
        (2773, 987, 2858, 1070),
        (2772, 1072, 2858, 1157),
        (2774, 1158, 2858, 1243)
    ]

    # 3. 创建目标文件夹
    base_path = r"C:\Users\GQX\DeltaForce\Assets\Sprite\ammo"
    save_dir = os.path.join(base_path, prefix)
    
    try:
        os.makedirs(save_dir, exist_ok=False)
        print(f"✅ 已创建文件夹: {save_dir}")
    except FileExistsError:
        print(f"⚠️ 文件夹已存在，将覆盖其中的同名文件: {save_dir}")

    # 4. 循环截图并保存
    print("\n开始截取屏幕...")
    success_count = 0
    
    for i, bbox in enumerate(bboxes, start=1):
        try:
            # 截取指定区域
            screenshot = ImageGrab.grab(bbox=bbox)
            
            # 构造文件名
            file_name = f"{prefix}_L{i}.png"
            save_path = os.path.join(save_dir, file_name)
            
            # 保存为 PNG
            screenshot.save(save_path, "PNG")
            print(f"  [{i}/6] 已保存: {save_path}")
            success_count += 1
            
        except Exception as e:
            print(f"  [{i}/6] ❌ 截取失败: {e}")

    # 5. 输出总结
    print("\n" + "="*40)
    print(f"任务完成！")
    print(f"成功截取: {success_count}/{len(bboxes)} 个文件")
    print(f"保存位置: {save_dir}")
    print("="*40)

if __name__ == "__main__":
    capture_ammo_sprites()