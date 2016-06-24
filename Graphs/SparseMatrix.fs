﻿namespace Graphs
open System
open Alea.CUDA
open Alea.CUDA.Unbound
open Microsoft.FSharp.Math
open System.Collections.Generic
open System.Linq
open MathNet.Numerics

(*
/// <summary>
/// Sparse matrix implementation with CSR and CSC storage
/// </summary>
[<StructuredFormatDisplay("{PrintMatrix}")>]
type SparseMatrix<'a> (ops : INumeric<'a>, row : 'a seq, rowIndex : int seq, colIndex : int seq, rowSize, isCSR : bool) =

    let mutable isCSR = isCSR
    let ops = ops
    let mutable rowIndex  = rowIndex.ToList()
    let mutable colIndex = colIndex.ToList()
    let mutable values = row.ToList()
    let mutable nnz = rowIndex.[rowIndex.Count - 1]
    let rowSize = rowSize

    let getRowColRange rowCol =
        if rowCol >= rowIndex.Count - 1 then
            failwith (if isCSR then "row index out of range" else "column index out of range")

        let rowStart = rowIndex.[rowCol]
        let rowEnd = rowIndex.[rowCol + 1] - 1

        rowStart, rowEnd

    let getNZCols row =
        let rowStart, rowEnd = getRowColRange row
        colIndex.GetRange(rowStart, rowEnd - rowStart + 1)

    let getNZVals row = 
        let rowStart, rowEnd = getRowColRange row
        values.GetRange(rowStart, rowEnd - rowStart + 1)

    let getRowCol row col =
        if col >= rowSize then failwith ( if isCSR then "column index out of range" else "row index out of range")

        let valCol = getNZCols row |> fun c -> c.IndexOf(col)
        let rowStart = rowIndex.[row]
        
        if valCol >= 0 then values.[rowStart + valCol] else ops.Zero

    let maxPrintVals = 20

    // Pretty print for printfn "%A"
    let printMatrix () =
        let rows, cols = if isCSR then rowIndex.Count - 1, rowSize else rowSize, rowIndex.Count - 1

        let printRows, printCols = 
            (if rows > maxPrintVals then maxPrintVals else rows), 
            if cols > maxPrintVals then maxPrintVals else cols

        let elipsesCols = if cols > maxPrintVals then "..." else ""
        let elipsesRows = if rows > maxPrintVals then "..." else ""

        [
            for i = 0 to printRows - 1 do
                yield sprintf "%A%s" 
                    [for j = 0 to printCols - 1 do yield if isCSR then getRowCol i j else getRowCol j i] elipsesCols
        ]
        |> List.reduce(fun acc e -> acc + "\n" + e)
        |> fun l -> l + "\n" + elipsesRows
                    
    member internal this.PrintMatrix = printMatrix()
    member this.GetNZValues = getNZVals
    member this.Cols = if isCSR then rowSize else rowIndex.Count - 1
    member this.Rows = if isCSR then rowIndex.Count - 1 else rowSize

    ///<summary>
    /// Returns a column (or a row) indices of nz elements
    ///</summary>                  
    member this.Item
        with get(row, col) = 
            if isCSR then getRowCol row col else getRowCol col row

    member this.Item
        with get row = getNZCols row

    member this.Transpose() = isCSR <- not isCSR

    member this.NNZ = nnz
    member this.AddValues (row : 'a []) =
        if row.Length <> rowSize then failwith "wrong number of elements in the row/column"
        let colIdx, vals = 
            Array.zip [|0..row.Length - 1|] row 
            |> Array.filter (fun (i, v) -> ops.Compare(v, ops.Zero) <> 0)
            |> Array.unzip
            
        values.AddRange(vals)
        colIndex.AddRange(colIdx)

        rowIndex.Add(rowIndex.[rowIndex.Count - 1] + vals.Length)
        nnz <- nnz + vals.Length

    static member CreateMatrix (row : 'a []) (isCSR : bool) =
        let ops = GlobalAssociations.GetNumericAssociation<'a>()
        let colIdx, vals = 
            Array.zip [|0..row.Length - 1|] row 
            |> Array.filter (fun (i, v) -> ops.Compare(v, ops.Zero) <> 0)
            |> Array.unzip

        SparseMatrix(ops, vals, [0; vals.Length], colIdx, row.Length, isCSR)
*)