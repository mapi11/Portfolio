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

## Валидатор ассетов

Бюджеты задаются в `Assets/_Project/Settings/AssetBudgets.asset` (ScriptableObject, правило выбирается по папке):

- **При импорте**: текстуры с суффиксом `_N` становятся Normal Map, `_MRAO`/`_ORM`/`_Mask` импортируются как linear, для Android и iOS ставится ASTC-оверрайд. Модели сразу проверяются на полигонаж, LOD, материалы, кости и веса, нарушения выводятся в консоль со ссылкой на ассет.
- **Окно** `Tools/Portfolio/Asset Validator`: проверка всей папки, переход к проблемному ассету в один клик.
- **CI**: код выхода 1, если есть ошибки:

```bash
Unity -batchmode -nographics -projectPath . -executeMethod Portfolio.Editor.Validation.AssetValidationCI.Run -quit -logFile -
```

## План работ

- [x] Проект URP (Unity 6000.0.78f1), Git LFS для исходников и текстур, структура папок
- [x] Документы: бюджеты, требования к ригу
- [x] Валидатор ассетов и автонастройка импорта
- [ ] **Оружие**: blockout → high → low → UV → запечка → Substance → LOD0–2 в бюджете
- [ ] **VFX стрельбы**: muzzle flash, трассер, гильзы, попадания (металл, бетон, дерево) + декали, пулы
- [ ] Замеры VFX на устройстве, заполнить Profiling.md
- [ ] **Риг**: кости оружия, FP-руки, IK левой руки, анимации idle/fire/reload
- [ ] **Шейдеры**: коллиматор, hit-flash, декали
- [ ] CI (GitHub Actions + GameCI) с запуском валидатора
- [ ] Видео 60–90 с, APK, breakdown на ArtStation

## Структура

```
Assets/_Project/
  Art/Weapons/<Name>/{Source,Meshes,Textures,Materials}
  Art/Characters/FPSArms
  Rigs/  VFX/{Muzzle,Impacts,Tracers,Shells}  Shaders/{Sights,Decals,HitFlash}
  Scenes/  Settings/  Scripts/{Runtime,Editor/Validation}
Docs/   бюджеты, требования к ригу, профилирование, скриншоты
```
