# WoW-Map-Asset-Parser
A work in progress tool for extracting all the used assets on a map so that only the required data needs to be packaged.

This tool only works with WOTLK 3.3.5 and requires all the data to be outside of MPQs but it works.

```bash
.\WoWResourceParser.exe
WoWResourceParser 1.0.0.0
Copyright ©  2021

ERROR(S):
  Required option 'adtFolder' is missing.
  Required option 'dataFolder' is missing.
  Required option 'destFolder' is missing.

  -e, --extract       Set whether to extract the assets used to assets.txt.

  -p, --package       Set whether to package the assets.txt to the destination folder.

  --assetsFilePath    Override the assets.txt file path

  --adtFolder         Required. Sets the ADT (map) folder to read from

  --dataFolder        Required. Sets the data folder to read assets from

  --destFolder        Required. Sets the destination folder for packaging to

  --help              Display this help screen.

  --version           Display version information.
  ```
Example usage:
```bash
.\WoWResourceParser.exe -e -p --adtFolder="E:\_NewProjectWoW\_HOCKA\mpqs\world\maps\DungeonMode" --dataFolder="E:\_NewProjectWoW\_HOCKA\mpqs" --destFolder="D:\WoW 3.3.5a\Data\patch-5.MPQ"
```
