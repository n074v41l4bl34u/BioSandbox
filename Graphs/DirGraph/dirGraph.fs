﻿namespace Graphs

open System.Collections.Generic
open System.Linq
open System.IO
open System
open DrawGraph
open System.Linq
open Alea.CUDA

#nowarn "25"

[<AutoOpen>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DirectedGraph =

    let internal hasCuda = 
        lazy (
            try
                Device.Default.Name |> ignore
                true
            with
            _ ->    false
        )

    let getWorker () = if hasCuda.Force() then Some(Device.Default) else None

    /// <summary>
    /// Instantiate a directed graph. Need number of vertices
    /// Format of the file:
    /// v -> a, b, c, d - v - unique vertex name for each line of the file. a, b, c, d - names of vertices it connects to.
    /// </summary>
    [<StructuredFormatDisplay("{AsEnumerable}")>]
    type DirectedGraph (rowIndex : int seq, colIndex : int seq, ?verticesNameToOrdinal : IDictionary<string, int>) = 

        let rowIndex  = rowIndex.ToArray()
        let colIndex = colIndex.ToArray()
        let nEdges = rowIndex.[rowIndex.Length - 1]
        let verticesNameToOrdinal = defaultArg verticesNameToOrdinal ([0..rowIndex.Length - 2].ToDictionary((fun s -> s.ToString()), id) :> IDictionary<string, int>)
        let nVertex = verticesNameToOrdinal.Count

        let ordinalToNames () =
            let res : string [] = Array.zeroCreate verticesNameToOrdinal.Count
            verticesNameToOrdinal 
            |> Seq.iter (fun kvp -> res.[kvp.Value] <- kvp.Key)

            res

        let verticesOrdinalToNames = ordinalToNames()

        let nameFromOrdinal = fun ordinal -> verticesOrdinalToNames.[ordinal]
        let ordinalFromName = fun name -> verticesNameToOrdinal.[name]

        let getVertexConnections ordinal =
            let start = rowIndex.[ordinal]
            let end' = rowIndex.[ordinal + 1] - 1
            colIndex.[start..end']

        let asOrdinalsEnumerable () =
            Seq.init nVertex (fun i -> i, getVertexConnections i)

        let reverse =
            lazy (
                let allExistingRows = [0..rowIndex.Length - 1]                

                let grSeq = 
                    asOrdinalsEnumerable ()
                    |> Seq.map (fun (i, verts) -> verts |> Seq.map (fun v -> (v, i)))
                    |> Seq.collect id
                    |> Seq.groupBy fst
                    |> Seq.map (fun (key, sq) -> key, sq |> Seq.map snd |> Seq.toArray)

                let allRows : seq<int * int []> = 
                    allExistingRows.Except (grSeq |> Seq.map fst) |> Seq.map (fun e -> e, [||])
                    |> fun col -> col.Union grSeq
                    |> Seq.sortBy fst
                    

                let revRowIndex = allRows |> Seq.scan (fun st (key, v) -> st + v.Length) 0
                let revColIndex = allRows |> Seq.collect snd

                DirectedGraph(revRowIndex, revColIndex, verticesNameToOrdinal)            
            )
        
        /// <summary>
        /// Create the graph from an array of strings
        /// </summary>
        /// <param name="lines">
        /// array of strings formatted: out_vertex -> in_v1, in_v2, in_v3,..
        ///</param>
        static member FromStrings (lines : string seq) =  

            let rowIndexRaw = List<int>()
            let colIndex = List<int>()

            let nameToOrdinal = Dictionary<string, int>() // vertices and the index to which they correspond

            let addVertex (line : string) =
                
                let vertex, connected = 
                    if line.Contains("->") then
                        line.Trim().Split([|"->"|], 2, StringSplitOptions.RemoveEmptyEntries) |> fun [|a; b|] -> a.Trim(), b.Trim()
                    else line.Trim(), ""

                let newVertex = not (nameToOrdinal.ContainsKey vertex)
                if newVertex then 
                    nameToOrdinal.Add(vertex, nameToOrdinal.Keys.Count)                    
                    rowIndexRaw.Add 0

                // get vertices connected to this one
                let connectedVertices =
                    if not (String.IsNullOrEmpty connected) then
                        connected.Split([|','|], StringSplitOptions.RemoveEmptyEntries)
                        |> Array.map (fun e -> e.Trim())
                    else [||]

                let ordinal = nameToOrdinal.[vertex]

                // store all the vertices we have not seen before
                let newVertices = connectedVertices.Except nameToOrdinal.Keys |> Seq.toArray
                newVertices
                |> Seq.iter (fun v -> nameToOrdinal.Add(v, nameToOrdinal.Keys.Count))
                
                // extend the new rows
                let newVerticesZeroes = Array.zeroCreate newVertices.Length
                rowIndexRaw.AddRange newVerticesZeroes

                // for now we will store the number of vertices in the row index
                // entry for the given row. We will need to scan it and update its values later
                rowIndexRaw.[nameToOrdinal.[vertex]] <- connectedVertices.Length

                let connectedOrdinals = 
                    connectedVertices
                    |> Seq.map (fun v -> nameToOrdinal.[v])

                // if we are inserting a "retoractive" row, we need to know where we are inserting it!
                if newVertex then colIndex.AddRange connectedOrdinals
                else
                    let rowIndexCur = rowIndexRaw |> Seq.scan (+) 0 |> Seq.toList
                    colIndex.InsertRange(rowIndexCur.[ordinal], connectedOrdinals)

            lines |> Seq.iter addVertex

            DirectedGraph(rowIndexRaw |> Seq.scan (+) 0, colIndex, nameToOrdinal)


        /// <summary>
        /// Create the graph from a file
        /// </summary>
        /// <param name="fileName"></param>
        static member FromFile (fileName : string) =

            if String.IsNullOrWhiteSpace fileName || not (File.Exists fileName) then failwith "Invalid file"
            
            let lines = File.ReadLines(fileName)
            DirectedGraph.FromStrings(lines)                

        member this.Vertices = nVertex
        member this.Item
            with get vertex = ordinalFromName vertex |> getVertexConnections |> Array.map nameFromOrdinal

        member this.AsEnumerable = Seq.init nVertex (fun n -> nameFromOrdinal n, this.[nameFromOrdinal n])
        member this.Subgraph (vertices : string list) = Seq.init (vertices.Count()) (fun i -> vertices.[i], this.[vertices.[i]])

        member this.Reverse = reverse.Force()
        member private this.RowIndex = rowIndex
        member private this.ColIndex = colIndex

        member private this.GetConnectedVertices ordinal = getVertexConnections ordinal
        member private this.GetConnectedToMe ordinal = this.Reverse.GetConnectedVertices ordinal
        member private this.GetAllConnections ordinal =
            this.GetConnectedVertices ordinal |> fun g -> g.Union (this.GetConnectedToMe ordinal)

        member this.Connected vertex = vertex |> ordinalFromName |> this.GetConnectedVertices |> Array.map nameFromOrdinal
        member this.ConnectedToMe vertex = vertex |> ordinalFromName |> this.GetConnectedToMe |> Array.map nameFromOrdinal
        member this.AllConnected vertex = vertex |> ordinalFromName |> this.GetAllConnections |> Seq.map nameFromOrdinal |> Seq.toArray

        /// <summary>
        /// Is this a Eulerian graph: i.e., in-degree of all vertices = out-degree
        /// </summary>
        //TODO: Add fully connected check
        member this.IsEulerian =
            let gr = this.AsEnumerable |> Seq.map (fun (v, into) -> v, into.Length) |> Seq.toArray
            let rev = this.Reverse.AsEnumerable |> Seq.map (fun (v, into) -> v, into.Length) |> Seq.toArray

            if gr.Length <> rev.Length then false
            else
                Array.zip (Array.sortBy fst gr) (Array.sortBy fst rev)
                |> Array.exists (fun ((v, into), (v1, into1)) -> v <> v1 || into <> into1)
                |> not

        
        /// <summary>
        /// 0 -> 1 -> 2 -> 3 -> ... -> n -> 0
        /// </summary>
        /// <param name="n">Index of the last vertex</param>
        static member GenerateSimpleLoop n =
            let rowIndex = [0..n]
            let colIndex = [1..n - 1] @ [0]
            DirectedGraph(rowIndex, colIndex)
        
        /// <summary>
        /// Generates a Eulerian graph
        /// </summary>
        /// <param name="n"> number of vertices </param>
        /// <param name="k"> max number of connections in one direction</param>
        static member GenerateEulerGraph n k =
            let rnd = Random(int DateTime.Now.Ticks)
            let connections = Array.init n (fun i -> rnd.Next(1, k + 1))
            let connectionsReverse = Array.zeroCreate n
            connections.CopyTo(connectionsReverse, 0)
            let rowIndex = [0].ToList()
            let colIndex = List<int>()
            for i = 0 to n - 1 do
                rowIndex.Add(0)
                rowIndex.[i + 1] <- rowIndex.[i] + connections.[i]
                
                // scan vertices starting from vertex i and grab the next available vertex to connect to while it is possible 
                // connectionsReverse keeps track of each vertex ability to serve as an inbound vertex. At the end, all
                // of its elements should be eq to 0
                let cols = 
                    (0, 1)
                    |> Seq.unfold 
                        (fun (st, k) -> 
                            let idx = (i + k) % n
                            if st = connections.[i] then None 
                            elif connectionsReverse.[idx] = 0 
                            then Some(-1, (st, k + 1))
                            else
                                connectionsReverse.[idx] <- connectionsReverse.[idx] - 1
                                Some(idx, (st + 1, k + 1)))
                    |> Seq.filter(fun x -> x >= 0)
                colIndex.AddRange cols    
            DirectedGraph(rowIndex, colIndex)
        
        /// <summary>
        /// Finds all connected components and returns them as a list of vertex sets.
        /// </summary>
        member this.FindConnectedComponents () =
            let rnd = Random()
            let mutable vertices = Enumerable.Range(0, this.Vertices) |> Seq.toList
            
            [
                while vertices.Count() > 0 do
                    let idx = vertices.[rnd.Next(vertices.Count())]
                    
                    let connected = 
                        List.unfold 
                            (fun (prevVertices : HashSet<int>, visited) -> 
                                let visitVerts = 
                                    visited 
                                    |> Array.map (this.GetAllConnections >> Seq.toArray) 
                                    |> Array.concat |> Array.distinct
                                    |> fun a -> a.Except prevVertices |> Seq.toArray
                                let prevLen = prevVertices.Count                                        
                                visitVerts |> Array.iter (prevVertices.Add >> ignore)
                                if prevVertices.Count = prevLen then None
                                else Some(prevVertices, (prevVertices, visitVerts))
                            ) (HashSet<int>([idx]), [|idx|]) 
                        |> List.take 1
                        |> List.exactlyOne

                    vertices <- vertices.Except connected |> Seq.toList
                    yield connected |> Seq.map (fun i -> verticesOrdinalToNames.[i]) |> Seq.toList
            ]
                                                    
        member private this.Worker = getWorker()

        override this.Equals g2 =
            match g2 with
            | :? DirectedGraph as g ->
                if g.Vertices = this.Vertices then

                    let grSort (gr : seq<string * string[]>) =
                        gr |> Seq.sortBy fst

                    let gseq = g.AsEnumerable |> grSort
                    let thisSeq = this.AsEnumerable |> grSort

                    let getKeys (gr : seq<string*string[]>) =
                        gr |> Seq.map fst |> Seq.toArray
                
                    let getVals (gr : seq<string * string[]>) =
                        gr |> Seq.map (fun (a, b) -> b |> Array.sort) |> Seq.toArray

                    let valuesEq (gr : seq<string * string []>) (gr1 : seq<string * string []>) =
                        not (Seq.zip (getVals gr) (getVals gr1) |> Seq.exists (fun (a, b) -> a <> b))

                    let keysEq (gr : seq<string * string []>) (gr1 : seq<string * string []>) =
                        (getKeys gr) = (getKeys gr1)

                    keysEq thisSeq gseq && valuesEq thisSeq gseq
                else false
               
            | _ -> false
            
        override this.GetHashCode() = this.AsEnumerable.GetHashCode() 
                    