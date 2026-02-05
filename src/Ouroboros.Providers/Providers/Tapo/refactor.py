import re

# Fix URL constructions - change & to ? for first parameter
files = ['TapoLightStripOperations.cs', 'TapoPlugOperations.cs']

for file in files:
    with open(file, 'r') as f:
        content = f.read()
    
    # Fix all &level= to ?level=
    content = re.sub(r'(l9\d+/set-brightness)&(level=)', r'\1?\2', content)
    # Fix all &hue= to ?hue=
    content = re.sub(r'(l9\d+/set-hue-saturation)&(hue=)', r'\1?\2', content)
    # Fix all &color_temperature= to ?color_temperature=
    content = re.sub(r'(l9\d+/set-color-temperature)&(color_temperature=)', r'\1?\2', content)
    # Fix all &lighting_effect= to ?lighting_effect=
    content = re.sub(r'(l9\d+/set-lighting-effect)&(lighting_effect=)', r'\1?\2', content)
    # Fix all &start_date= to ?start_date=
    content = re.sub(r'(p\d+/get-[^"]+)&(start_date=)', r'\1?\2', content)
    
    with open(file, 'w') as f:
        f.write(content)

print("Fixed URL constructions")
