# DCC

Исходники для Blender. Папка вне `Portfolio/Assets`, чтобы Unity не пыталась импортировать `.blend`.

## FP-руки с AK47

```bash
blender --background --factory-startup --python DCC/Blender/build_fps_rig.py -- .
```

Собирает `DCC/Blender/FPSArms_AK47.blend` и экспортирует `Portfolio/Assets/_Project/Art/Characters/FPSArms/SK_FPSArms_AK47.fbx`. Проверено на Blender 3.5.

После экспорта в Unity: `Tools/Portfolio/Install FPS Arms In Weapon Range`.

## Исходные модели (CC0)

- Руки: [male base mesh arms](https://orange-juice-games.itch.io/male-base-mesh-arms), Orange Juice Games
- AK47: [12 Low Poly Guns](https://gintoki1234.itch.io/12-low-poly-guns), Gintoki1234
