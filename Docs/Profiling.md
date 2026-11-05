# Профилирование: до и после

Заполняется по мере работы. Каждая строка подтверждается скриншотом в `Docs/Images/` (Profiler, Frame Debugger, RenderDoc, AGI, Xcode GPU Capture).

Устройство: _модель, GPU, RAM_; Билд: _IL2CPP, Release, URP Mobile_RPAsset_; Сценарий: _30 с непрерывной стрельбы по смешанным поверхностям_

Сценарий запускается одинаково на всех устройствах: кнопка "Benchmark" в HUD, клавиша B или из командной строки. Итог (среднее время кадра, 1 % low, число выстрелов) пишется в лог:

```bash
WeaponRange.exe -benchmark -benchmark-duration 30 -screenshot shot.png
```

На Android: кнопка "Benchmark" в HUD, итог смотреть в `adb logcat -s Unity`.

Проверка на ПК (RTX 5080, 1280x720, только смоук-тест, для мобильного бюджета не показательна): 10 с, 117 выстрелов, 0,61 мс в среднем, 1 % low 673 fps.

| Метрика | До | После | Что сделано | Скриншот |
|---|---|---|---|---|
| FPS (медиана / 1 % low) | | | | |
| GPU frame time, мс | | | | |
| Draw calls / SetPass | | | | |
| Overdraw VFX (макс. слоёв) | | | | |
| Память текстур, МБ | | | | |
| Shader variants в билде | | | | |
| FPS после 30 мин (thermal) | | | | |
