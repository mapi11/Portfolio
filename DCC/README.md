# DCC

Исходники для Blender. Папка вне `Portfolio/Assets`, чтобы Unity не пыталась импортировать `.blend`.

## AK47

```bash
blender --background --factory-startup --python DCC/Blender/prepare_ak47.py -- .
```

Масштаб до реальных 0.88 м, пивот на пистолетной рукояти, LOD0–2 через Decimate, сокеты `socket_muzzle` и `socket_shell_eject`. Сохраняет `DCC/Blender/AK47.blend` и экспортирует `Portfolio/Assets/_Project/Art/Weapons/AK47/SM_AK47.fbx`. Проверено на Blender 3.5.

После экспорта в Unity: `Tools/Portfolio/Install AK47 In Weapon Range`.

Исходная модель (CC0): [12 Low Poly Guns](https://gintoki1234.itch.io/12-low-poly-guns), Gintoki1234.
