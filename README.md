# XYTidDatabase

```ps1
# Create database
XYTidDatabase.exe create --tid 0 --sid 28552 --timeout 3030 --start $(0xB2B6FFFBU) --end $(0xD000000U)

# Search from database
XYTidDatabase.exe search --path 0-28552.dat --base-seed $(0xB281A4EEU) --min-advance 30 --max-advance 2000 --count 3
```
