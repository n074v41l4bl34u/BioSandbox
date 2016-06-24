﻿// Learn more about F# at http://fsharp.org. See the 'F# Tutorial' project
// for more guidance on F# programming.

#r @"..\..\packages\FsCheck\lib\net45\FsCheck.dll"
#r @"..\..\packages\Alea.CUDA\lib\net40\Alea.CUDA.dll"
#r @"..\..\packages\Alea.CUDA.Unbound\lib\net40\Alea.CUDA.Unbound.dll"
#r @"..\..\Graphs\DrawGraph\bin\Debug\DrawGraph.dll"
#I @"..\..\packages\Alea.CUDA\lib\net40\"
#I @"..\..\packages\Alea.CUDA.Unbound\lib\net40"

#r @"Alea.CUDA"
#r @"Alea.CUDA.Unbound"

#load "dirGraph.fs"
open Graphs
open FsCheck
open System
open System.Text.RegularExpressions
open System.Diagnostics

//let nucl = Gen.oneof [gen {return 'A'}; gen {return 'C'}; gen {return 'G'}; gen {return 'T'}]
let nucl = Gen.choose(int 'A', int 'Z') |> Gen.map char

let genVertex len =  Gen.arrayOfLength len nucl |> Gen.map (fun c -> String(c))
let vertices len number = Gen.arrayOfLength number (genVertex len) |> Gen.map Array.distinct

let graphGen len number =
    let verts = vertices len number
    let rnd = Random(int DateTime.UtcNow.Ticks)
    let pickFrom = verts |> Gen.map (fun lst -> lst.[rnd.Next(lst.Length)])
    let pickTo = Gen.sized (fun n -> Gen.listOfLength (if n = 0 then 1 else n) pickFrom)

    Gen.sized
    <| 
    (fun n ->
        Gen.map2 
            (fun from to' -> 
                from, (to' |> Seq.reduce (fun acc v -> acc + ", " + v))) pickFrom pickTo
        |>
        Gen.arrayOfLength (if n = 0 then 1 else n)
        |> Gen.map (Array.distinctBy fst)
        |> Gen.map (fun arr ->  arr |> Array.map (fun (a, b) -> a + " -> " + b))
    )
    |> Gen.map DirectedGraph<string>.FromStrings


type Marker =
    static member digr = graphGen 3 500 |> Arb.fromGen
    static member ``Reverse of the Reverse equals itself`` (gr : DirectedGraph<string>) =
        gr.Reverse.Reverse = gr

Arb.registerByType(typeof<Marker>)
Check.QuickAll(typeof<Marker>)