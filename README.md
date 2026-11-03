# Technical Art Portfolio

Pet-проекты Technical Artist на Unity 6 (URP): VFX, шейдеры, риги, инструменты для художников и оптимизация под мобильные устройства.

**Максим Астраханцев** · Telegram [@mapiAP](https://t.me/mapiAP)

## Проекты

### Weapon Range: VFX стрельбы

![Weapon Range](Docs/Images/weapon_range_win.png)

Тир для мобильного шутера: вспышка выстрела, трассеры, гильзы, попадания по металлу, бетону и дереву, следы от пуль. Эффекты укладываются в бюджет слабого Android: пулы, инстансинг, свои HLSL-шейдеры.

Сцена: `Assets/_Project/Scenes/WeaponRange.unity`

### Grass Field: трава с ветром

![Grass Field](Docs/Images/grass_field.png)

Поле травы для мобильных: ~50 тыс. травинок за 2 draw calls, волны ветра, отталкивание от объектов, просвет против солнца. Без альфа-теста и compute-шейдеров. [Подробнее](Docs/Grass.md)

Сцена: `Assets/_Project/Scenes/GrassField.unity`

### Winter Glade: cartoon зима

![Winter Glade](Docs/Images/winter_glade.png)

Мультяшная зимняя поляна: toon-шейдер со снежными шапками и искрящимся снегом, лёд с глубиной трещин без прозрачности, ёлка, трава из-под снега, снегопад. [Подробнее](Docs/Winter.md)

Сцена: `Assets/_Project/Scenes/WinterGlade.unity`

### LOD Demo: уровни детализации

![LOD Demo](Docs/Images/lod_demo.png)

Дом, машина и дерево с LOD0–2. Камера сама отъезжает и возвращается, HUD показывает текущий уровень и треугольники каждой модели, по T модели подсвечиваются цветом уровня. LOD сделаны в Blender через Decimate, у мелких объектов свои пороги переключения.

Сцена: `Assets/_Project/Scenes/LODDemo.unity`

## Инструменты для художников

Editor-окна в меню `Tools/Portfolio`. Работают только в редакторе и в билд не попадают.

### Material Replacer

![Material Replacer](Docs/Images/tool_material_replacer.png)

Замена материала на объекте сцены: перетащить объект из Hierarchy, материал из Project и нажать кнопку. Заменяет все слоты, по желанию вместе с дочерними объектами, откатывается через Ctrl+Z.

`Tools/Portfolio/Material Replacer`

### Asset Validator

Проверка 3D-ассетов по бюджетам (полигоны, LOD, кости, текстуры, именование) при импорте, в окне редактора и в CI.

`Tools/Portfolio/Asset Validator`

## Документы

- [Бюджеты](Docs/Budgets.md)
- [Профилирование](Docs/Profiling.md)

## Как открыть

Unity 6000.0.78f1. В Unity Hub добавить папку `Portfolio/`.
