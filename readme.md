# Imp Maps Downloader A1

Made by Skylark "Help! Raccoons took my penis!" Murphy

This application will download all maps on the maplist, unzip, and place them in your `tf\download\maps` folder ready for use in the imp to reduce wait times on map change.

## How to use

1. Run `ImpMapsDownloader.exe`
2. Current maps which you don't already have installed will be installed and there is a readout on progress
3. By default step 2 is done every 5 minutes, this can be changed in `config.json` under `interval` - note, the shortest time is 5 minutes, if you set this to `0` it will not refresh

## Errors and you

### ERR01

> tf\download\maps directory not found, please enter the full path of your maps download folder

If the `tf\download\maps` folder cannot be found automatically you will get this error, find your `tf\download\maps` folder (**not** `tf\maps`), paste into the window, and press enter

### ERR02

> Defined tf\download\maps directory not found, please enter the full path of your maps download folder

Same thing as in ERR01, but with the directory previously given.

### ERR03

> HtmlAgilityPack error

There was an error with HtmlAgilityPack which is being used to scrape the website, this has probably arisen from the website being changed, a new `selector` in config is probably needed.
Refer back to the github page, the current selector is: `"body .row.mt-3 .col-12.mb-3 .card div table tr td:first-child a"`
