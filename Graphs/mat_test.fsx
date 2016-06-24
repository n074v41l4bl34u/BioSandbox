// Learn more about F# at http://fsharp.org. See the 'F# Tutorial' project
// for more guidance on F# programming.
#r @"..\packages\Alea.CUDA\lib\net40\Alea.CUDA.dll"
#r @"..\packages\Alea.CUDA.IL\lib\net40\Alea.CUDA.IL.dll"
#r @"..\packages\Alea.CUDA.Unbound\lib\net40\Alea.CUDA.Unbound.dll"
#r @"..\packages\Alea.IL\lib\net40\Alea.IL.dll"
//#r @"C:\Program Files (x86)\FSharpPowerPack-4.0.0.0\bin\FSharp.PowerPack.dll"

// Testing
#r @"..\packages\NUnit\lib\net45\nunit.framework.dll"
#r @"..\packages\FsCheck\lib\net45\FsCheck.dll"

#r @"..\packages\MathNet.Numerics\lib\net40\MathNet.Numerics.dll"
#r @"..\packages\MathNet.Numerics.FSharp\lib\net40\MathNet.Numerics.FSharp.dll"
open MathNet.Numerics.LinearAlgebra
//#load "SparseMatrix.fs"
//open Graphs

// Testing
open FsCheck
open System

let genZero = gen {return 0}
let zeroOrNot = Gen.frequency [(3, genZero); (1, Gen.choose(1, 10))]

let generateRow  len = Gen.arrayOfLength len zeroOrNot
let generateZeroRow len = Gen.arrayOfLength len genZero
let genSingleRow len = Gen.frequency [(7, generateRow len); (1, generateZeroRow len)]

//type SM = Matrix<int>.create// SparseMatrix<int>
open Microsoft.FSharp.Collections

type Generators =
    static member SparseMatrix () =
        let matrix =
            gen {
                let! csr = Gen.oneof[gen {return true}; gen {return false}]
                let! len = Gen.choose(1, 1000)
                let! s = Gen.choose(1, 1000)

                let rows = Gen.listOfLength s (genSingleRow len)
                let! matrix = 
                    Gen.map
                        (fun (r : int [] list) -> 
                            let rl = Array.length r.[0]
                            let cl = List.length r
                            let m = SparseMatrix.create rl cl 0 //   SparseMatrix.CreateMatrix r.[0] csr
                            let mutable i=0;
                            for row in r.[1..] do
                                m.SetRow( i, row)
                                i<-i+1
                            m) rows
                return matrix
            }
        
        matrix |> Arb.fromGen

Arb.register<Generators>()

// Define your library scripting code here
//let mR = SparseMatrix.CreateMatrix sampleRows.[0] true

