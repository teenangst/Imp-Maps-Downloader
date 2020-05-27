module DownloadMap

open Config
open System
open System.IO
open System.Text.RegularExpressions
open BlackFox.ColoredPrintf
open System.Net

let temp = Path.GetTempPath()

//tf\download\maps directory, with fallback if that folder isn't in the normal place
let rec verifyMapDirectory () =
  let enterDirectory () =
    colorprintf "> $cyan"
    config.mapdirectory <- Console.ReadLine()
    saveConfig()
    verifyMapDirectory()

  if config.mapdirectory <> null then
    if Directory.Exists config.mapdirectory |> not then
      colorprintfn "$red[ERR02] : Defined tf\download\maps directory not found, please enter the full path of your maps download folder"
      enterDirectory ()
      else config.mapdirectory
  else
    if Directory.Exists @"C:\Program Files (x86)\Steam\steamapps\common\Team Fortress 2\tf\download\maps\" |> not then //Default directory
      colorprintfn @"$red[ERR01] : tf\download\maps directory not found, please enter the full path of your maps download folder"
      enterDirectory ()
    else @"C:\Program Files (x86)\Steam\steamapps\common\Team Fortress 2\tf\download\maps\"
let mutable downloadMapDirectory = verifyMapDirectory()

//Get maps from download directory
let rec fetchDownloadedMaps () = 
  try
    Directory.GetFiles downloadMapDirectory |> Array.map (fun x -> (new Regex("[^\\\\]*(?=\.bsp)")).Match(x).Value)
  with
  | _ -> //Invalid download directory, get correct directory and try again
    downloadMapDirectory <- verifyMapDirectory ()
    fetchDownloadedMaps()

let mutable maps:string[] = fetchDownloadedMaps () //Array of maps that are in the download directory, this is updated with new maps

//Download map, map name does not contain a filetype, i.e. `ctf_raccoons_mc3_a3`
let downloadMap gameday map =
  if (maps |> Array.contains map || File.Exists (Path.Combine(downloadMapDirectory, sprintf "%s.bsp" map))) |> not (*|| gameday <> null*) then //REMOVE
    let gds = if gameday <> null then sprintf " (%s)" gameday else ""
    colorprintf "$yellow[Downloading] %s$darkgray[%s]" map gds
    try
      (new WebClient()).DownloadFile(Path.Combine(config.cdn, sprintf "%s.bsp.bz2" map), Path.Combine(temp, sprintf "%s.bsp.bz2" map)) //Save bz2 to temp directory
      Console.CursorLeft <- 0
      colorprintf "$cyan[Unzipping]   %s$darkgray[%s]" map gds
      try 
        ICSharpCode.SharpZipLib.BZip2.BZip2.Decompress( //Decompress bz2 into download directory
          new FileStream(Path.Combine(temp, sprintf "%s.bsp.bz2" map), FileMode.Open), 
          new FileStream(Path.Combine(downloadMapDirectory, sprintf "%s.bsp" map), FileMode.CreateNew), 
          true)
        Console.CursorLeft <- 0
        colorprintfn "$green[Complete]    %s$darkgray[%s]" map gds
        maps <- Array.append maps [|map|] //Add map to downloaded maps
        File.Delete(Path.Combine(temp, sprintf "%s.bsp.bz2" map)) //Delete temp
      with
      | _ -> //Collision or download directory wrong, verify the map directory exists and update map list - downloading will attempt again next tick
        Console.CursorLeft <- 0
        colorprintfn "$red[Error]       %s$darkgray[%s]" map gds
        downloadMapDirectory <- verifyMapDirectory ()
        maps <- fetchDownloadedMaps ()
        try 
          File.Delete(Path.Combine(temp, sprintf "%s.bsp.bz2" map))
        with
        | _ -> ignore |> ignore
    with
    | _ -> 
      Console.CursorLeft <- 0
      colorprintfn "$red[Invalid]     %s$darkgray[%s]" map gds
      maps <- Array.append maps [|map|]
  //else
  //  colorprintfn "$red[Skipping]    %s" map
let downloadMaps gameday mapArr = 
  mapArr |> Array.filter (fun x -> maps |> Array.exists(fun y -> y = x) |> not) |> Array.iter (fun x -> downloadMap gameday x)
  