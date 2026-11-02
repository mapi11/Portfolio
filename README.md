# FP Weapon Showcase: Technical Art для мобильного шутера

Портфолио-проект Technical Artist (Unity 6, URP). Вертикальный срез FP-оружия для мобильного шутера: от исходника в DCC до стабильного кадра на бюджетном Android, с цифрами профилирования на каждом шаге.

**Максим Астраханцев** · Technical Artist (Unity) · Telegram [@mapiAP](https://t.me/mapiAP)

## Что показывает проект

| Область | Что сделано | Где смотреть |
|---|---|---|
| Арт-пайплайн | Blender → Substance Painter → Unity, hard-surface, ретопология, 3 LOD, channel packing (MRAO), texel density | `Assets/_Project/Art/Weapons` |
| VFX стрельбы | Вспышка, трассеры, гильзы, попадания по материалам поверхности, декали, всё в пулах и в бюджете | `Assets/_Project/VFX` |
| Риг | Риг оружия (затвор, магазин, спуск), FP-руки, two-bone IK левой руки, blend shapes | `Assets/_Project/Rigs`, [RigRequirements.md](Docs/RigRequirements.md) |
| Шейдеры | Коллиматорный прицел (parallax reticle), hit-flash, декали попаданий: Shader Graph + HLSL, контроль variants | `Assets/_Project/Shaders` |
| Оптимизация | Бюджеты на кадр и на эффект, замеры на устройстве до и после | [Budgets.md](Docs/Budgets.md), [Profiling.md](Docs/Profiling.md) |
| Инструменты | Валидатор ассетов (AssetPostprocessor + EditorWindow + CI), автонастройка импорта текстур | `Assets/_Project/Scripts/Editor/Validation` |

## Тир и VFX стрельбы

![Тир: стрельба по бетону, дым и гильзы](Docs/Images/weapon_range_win.png)

Сцена `Assets/_Project/Scenes/WeaponRange.unity` пересобирается командой `Tools/Portfolio/Build Weapon Range`: текстуры, материалы, префабы эффектов и библиотека попаданий генерируются кодом. Сейчас вместо оружия стоит заглушка из примитивов. Сокеты `socket_muzzle` и `socket_shell_eject` названы по [RigRequirements.md](Docs/RigRequirements.md), поэтому модель с ригом встанет на их место без правок кода.

Управление: ЛКМ или тап — стрельба, ПКМ + мышь или второй палец — обзор, B — бенчмарк.

| Система | Как сделано | Почему так на мобильных |
|---|---|---|
| Вспышка и попадания (`BurstEffect`) | Системы частиц без emission, частицы выпускаются через `Emit` с явным min/max на событие. Один экземпляр на тип поверхности, симуляция в мировом пространстве | Без `Instantiate` и GC во время стрельбы, число частиц на событие ограничено бюджетом |
| Поверхности (`Surface`, `ImpactLibrary`) | Тип на коллайдере, эффект и декаль в ScriptableObject | Художник меняет эффекты, не трогая код оружия |
| Следы от пуль (`DecalSystem`) | Кольцевой буфер матриц, `Graphics.RenderMeshInstanced`, depth offset в шейдере | Один draw call на поверхность. URP Decal Projector требует DBuffer или depth prepass |
| Трассеры (`TracerSystem`) | Квады, развёрнутые к камере вокруг оси полёта, инстансинг. Трассер на каждый 2-й выстрел | Один draw call на все трассеры, без LineRenderer и GameObject на пулю |
| Гильзы (`ShellSystem`) | Своя баллистика: один рейкаст на гильзу за кадр, отскок, укладка на бок, инстансинг | Без Rigidbody, физика не растёт от темпа стрельбы |
| Шейдеры (`Portfolio/VFX/*`) | HLSL, premultiplied alpha: один режим блендинга для огня и дыма, `_Additive` выбирает сложение или смешивание | Совместимы с SRP Batcher, без soft particles (им нужна depth texture) |
| Текстуры | Атлас частиц 2x2 и атлас следов 2x2, ячейка выбирается тайлингом материала, ASTC 6x6 | Две текстуры на все эффекты |

Замер на устройстве: собрать `Tools/Portfolio/Build/Android` и запустить сценарий из [Profiling.md](Docs/Profiling.md).

## Валидатор ассетов

Бюджеты задаются в `Assets/_Project/Settings/AssetBudgets.asset` (ScriptableObject, правило выбирается по папке):

- **При импорте**: текстуры с суффиксом `_N` становятся Normal Map, `_MRAO`/`_ORM`/`_Mask` импортируются как linear, для Android и iOS ставится ASTC-оверрайд. Модели сразу проверяются на полигонаж, LOD, материалы, кости и веса, нарушения выводятся в консоль со ссылкой на ассет.
- **Окно** `Tools/Portfolio/Asset Validator`: проверка всей папки, переход к проблемному ассету в один клик.
- **CI**: код выхода 1, если есть ошибки:

```bash
Unity -batchmode -nographics -projectPath Portfolio -executeMethod Portfolio.Editor.Validation.AssetValidationCI.Run -quit -logFile -
```

## План работ

- [x] Проект URP (Unity 6000.0.78f1), Git LFS для исходников и текстур, структура папок
- [x] Документы: бюджеты, требования к ригу
- [x] Валидатор ассетов и автонастройка импорта
- [x] **Тир и VFX стрельбы**: вспышка, трассеры, гильзы, попадания по металлу, бетону и дереву, следы от пуль, бенчмарк
- [ ] **Оружие**: blockout → high → low → UV → запечка → Substance → LOD0–2 в бюджете, заменить заглушку
- [ ] Финальные текстуры VFX (вместо процедурных), свет от вспышки на high-пресете
- [ ] Замеры VFX на устройстве, заполнить Profiling.md
- [ ] **Риг**: кости оружия, FP-руки, IK левой руки, анимации idle/fire/reload
- [ ] **Шейдеры**: коллиматор, hit-flash, декали
- [ ] CI (GitHub Actions + GameCI) с запуском валидатора
- [ ] Видео 60–90 с, APK, breakdown на ArtStation

## Структура

```
Portfolio/                Unity-проект (открывать в Unity Hub эту папку)
 Assets/_Project/
   Art/Weapons/<Name>/{Source,Meshes,Textures,Materials}
   Art/Characters/FPSArms
   Rigs/  VFX/{Muzzle,Impacts,Tracers,Shells}  Shaders/{Sights,Decals,HitFlash}
   Scenes/  Settings/  Scripts/{Runtime,Editor/Validation}
Docs/   бюджеты, требования к ригу, профилирование, скриншоты
```
