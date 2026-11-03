# DCC

Исходники для Blender. Папка вне `Portfolio/Assets`, чтобы Unity не пыталась импортировать `.blend`.

## AK47

```bash
blender --background --factory-startup --python DCC/Blender/prepare_ak47.py -- .
```

Масштаб до реальных 0.88 м, пивот на пистолетной рукояти, LOD0–2 через Decimate, сокеты `socket_muzzle` и `socket_shell_eject`. Сохраняет `DCC/Blender/AK47.blend` и экспортирует `Portfolio/Assets/_Project/Art/Weapons/AK47/SM_AK47.fbx`. Проверено на Blender 3.5.

После экспорта в Unity: `Tools/Portfolio/Install AK47 In Weapon Range`.

## LOD Demo

```bash
blender --background --factory-startup --python DCC/Blender/prepare_lod_demo.py -- .
```

Дом, машина и дерево: один меш на модель, реальная высота, пивот в основании, LOD1 и LOD2 через Decimate. Экспорт в `Portfolio/Assets/_Project/Art/LODDemo/SM_<Name>.fbx` (части `_LOD0.._LOD2`, Unity сама собирает LODGroup).

После экспорта в Unity: `Tools/Portfolio/Build LOD Demo`.

Исходные модели (CC0): [Car Kit](https://kenney.nl/assets/car-kit), [City Kit Suburban](https://kenney.nl/assets/city-kit-suburban), [Nature Kit](https://kenney.nl/assets/nature-kit), Kenney.

Исходная модель AK47 (CC0): [12 Low Poly Guns](https://gintoki1234.itch.io/12-low-poly-guns), Gintoki1234.
