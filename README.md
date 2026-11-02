# Technical Art Portfolio

Pet-проекты Technical Artist на Unity 6 (URP): VFX, шейдеры, риги, инструменты для художников и оптимизация под мобильные устройства.

**Максим Астраханцев** · Telegram [@mapiAP](https://t.me/mapiAP)

## Проекты

### Weapon Range: VFX стрельбы

![Weapon Range](Docs/Images/weapon_range_win.png)

Тир для мобильного шутера: вспышка выстрела, трассеры, гильзы, попадания по металлу, бетону и дереву, следы от пуль. Эффекты укладываются в бюджет слабого Android: пулы, инстансинг, свои HLSL-шейдеры.

Сцена: `Assets/_Project/Scenes/WeaponRange.unity`

### Asset Validator

Проверка 3D-ассетов по бюджетам (полигоны, LOD, кости, текстуры, именование) при импорте, в окне редактора и в CI.

`Tools/Portfolio/Asset Validator`

## Документы

- [Бюджеты](Docs/Budgets.md)
- [Требования к ригу](Docs/RigRequirements.md)
- [Профилирование](Docs/Profiling.md)

## Как открыть

Unity 6000.0.78f1. В Unity Hub добавить папку `Portfolio/`.
