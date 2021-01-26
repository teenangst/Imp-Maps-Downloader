# Imp Maps Downloader A6

Made by [Skylark "Help! Raccoons took my penis!" Murphy](https://tf2maps.net/members/skylark.30345/)

This application will download all maps on the maplist, unzip, and place them in your `tf\download\maps` folder ready for use in the imp to reduce wait times on map change.

## How to use

1. Run `ImpMapsDownloader.exe`
2. Current maps which you don't already have installed will be installed and there is a readout on progress
3. By default step 2 is done every 5 minutes, this can be changed in `config.json` under `interval` - note, the shortest time is 5 minutes, if you set this to `0` it will not refresh

## GameDays

Lists of maps can now be synced from gameday indexes like [Gameday Index](https://raw.githubusercontent.com/teenangst/Imp-Maps-Downloader/master/gameday.json), this index is default but custom indexes can be added and shared. Simply add the URL to the index into the `gamedayindexendpoint` array inside the config.
When a new list is available you will be notified and asked if you would like to subscribe or not by pressing `Y` or `N`, these choices can be changed inside the config. Older expired lists will not be notified but you can subscribe to them by changing the `"subscribed"` value to `true`.

### Creating a new GameDay index

[You can watch a video on how to do steps 1-6](https://www.youtube.com/watch?v=yC4uhueUiQw)

1. Create either:
   - A file which contains a list of maps, one map on each line
   - A folder with bsps
2. Open `AIMDGameDayGenerator.exe`
3. enter the path to either the file or the directory. The GDG will then check to make sure all of the maps are available on the cdn, if they aren't this step will fail, make sure to get the maps uploaded then try again.
4. Enter the name for your GameDay list
5. Enter a date in which users who have added your GDL will be asked if they would like to subscribe (they are able to subscribe by editing their `config.json` after this date), dates can be in the form `mm-dd`, `yy-mm-dd`, or you can use `d`, `w`, `m` to define a relative time such as `3d` meaning 3 days from now. You can also enter in a custom unix time.
6. A GameDay index JSON will be created and the hash calculated
7. You can either just upload this file somewhere and share the link, or combine this with a previous index. If you combine indexes make sure to move the curly braced object into the square braces:

```json
[
  {
    "name":"A GameDay List",
    "expire": //etc
    //...
  },
  {
    "name":"Another GameDay List",
    "expire": //etc
    //...
  }
]
```

### Updating a GameDay index

[You can watch a video on how to do this](https://www.youtube.com/watch?v=ssW-Cj7CFBU)

1. Have a pre-generated GameDay index
2. Make any changes to the maps in the file
3. Paste the path to the JSON into GDG
4. The hash will be updated
5. Replace the online GameDay index with the new one

## Errors and you

### ERR01

> tf\download\maps directory not found, please enter the full path of your maps download folder

If the `tf\download\maps` folder cannot be found automatically you will get this error, find your `tf\download\maps` folder (**not** `tf\maps`), paste into the window, and press enter

### ERR02

> Defined tf\download\maps directory not found, please enter the full path of your maps download folder

Same thing as in ERR01, but with the directory previously given.

### ERR03

Error no longer occurs

### ERR04

> Unable to check if this is the latest version

The endpoint which is used to check for the latest version is unresponsive and will need to be checked manually.

### ERR05

> %URL% is not a valid gameday index

The URL stated is in `config.json` inside `"gamedayindexendpoint"` and is not a valid gameday index, a valid gameday index looks like [this](https://raw.githubusercontent.com/teenangst/Imp-Maps-Downloader/master/gameday.json).

### ERR06

> %GameDay% does not have the correct hash, please pass a path to the index into the GameDay Generator

When updating the GameDay index the hash was not recalculated, open `AIMDGameDayGenerator.exe` and past the path to the GameDay index, the hash will be made correct.

### ERR07

> Server ip %server ip% requires a port.

When adding servers ensure they have a port, i.e. `eu.tf2maps.net:27015` or `95.179.255.197:27015`

### ERR08

> Server %server ip% dns does not resolve.

The entered ip does not exist, if you try to join with the address it should fail; there was a mistake entering the address.
