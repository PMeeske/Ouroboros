#!/bin/bash
# Fix URL construction by using & instead of ? for action parameters
sed -i 's/\("l510\/set-brightness\?\)level/\1\&level/g' TapoLightOperations.cs
sed -i 's/\("l530\/set-brightness\?\)level/\1\&level/g' TapoLightOperations.cs
sed -i 's/\("l530\/set-color\?\)color/\1\&color/g' TapoLightOperations.cs
sed -i 's/\("l530\/set-hue-saturation\?\)hue/\1\&hue/g' TapoLightOperations.cs
sed -i 's/\("l530\/set-color-temperature\?\)color_temperature/\1\&color_temperature/g' TapoLightOperations.cs

sed -i 's/\("l900\/set-brightness\?\)level/\1\&level/g' TapoLightStripOperations.cs
sed -i 's/\("l900\/set-color\?\)color/\1\&color/g' TapoLightStripOperations.cs
sed -i 's/\("l900\/set-hue-saturation\?\)hue/\1\&hue/g' TapoLightStripOperations.cs
sed -i 's/\("l900\/set-color-temperature\?\)color_temperature/\1\&color_temperature/g' TapoLightStripOperations.cs

sed -i 's/\("l920\/set-brightness\?\)level/\1\&level/g' TapoLightStripOperations.cs
sed -i 's/\("l920\/set-color\?\)color/\1\&color/g' TapoLightStripOperations.cs
sed -i 's/\("l920\/set-hue-saturation\?\)hue/\1\&hue/g' TapoLightStripOperations.cs
sed -i 's/\("l920\/set-color-temperature\?\)color_temperature/\1\&color_temperature/g' TapoLightStripOperations.cs
sed -i 's/\("l920\/set-lighting-effect\?\)lighting_effect/\1\&lighting_effect/g' TapoLightStripOperations.cs

sed -i 's/\("p110\/get-hourly-energy-data\?\)start_date/\1\&start_date/g' TapoPlugOperations.cs
sed -i 's/\("p110\/get-daily-energy-data\?\)start_date/\1\&start_date/g' TapoPlugOperations.cs
sed -i 's/\("p110\/get-monthly-energy-data\?\)start_date/\1\&start_date/g' TapoPlugOperations.cs
