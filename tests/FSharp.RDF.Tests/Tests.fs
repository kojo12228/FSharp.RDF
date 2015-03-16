module FSharp.RDF.Tests

open Xunit
open Graph
open System.IO
open Swensen.Unquote

let functionalProperties = """
@prefix : <http://testing.stuff/ns#> .
@prefix rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#> .
@prefix xsd: <http://www.w3.org/2001/xmlschema#> .
@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .
@base <http://testing.stuff/ns> .

:item1 rdf:type :type1;
       :pr1 :item2 .

:item2 rdf:type :type2;
       :pr2 :item3 .

:item3 rdf:type :type3;
       :pr3 "avalue"^^xsd:string .
"""
let item1 = Uri.from "http://testing.stuff/ns#item1"
let type1 = Uri.from "http://testing.stuff/ns#type1"
let type2 = Uri.from "http://testing.stuff/ns#type2"
let item3 = Uri.from "http://testing.stuff/ns#item3"
let pr1 = Uri.from "http://testing.stuff/ns#pr1"
let pr2 = Uri.from "http://testing.stuff/ns#pr2"
let pr3 = Uri.from "http://testing.stuff/ns#pr3"

open Store
open resource

let g = Graph.from functionalProperties

[<Fact>]
let ``Pattern match subject``() =
  test <@ [ true ] = [ for r in (fromSubject item1 g) do
                         match r with
                         | Is item1 -> yield true ] @>

[<Fact>]
let ``Fail to pattern match subject``() =
  test <@ [ false ] = [ for r in (fromSubject item1 g) do
                          match r with
                          | Is item3 -> yield false
                          | _ -> yield true ] @>

[<Fact>]
let ``Pattern match type``() =
  test <@ [ true ] = [ for r in (fromSubject item1 g) do
                         match r with
                         | HasType type1 _ -> yield true
                         | _ -> yield false ] @>

[<Fact>]
let ``Fail to pattern match type``() =
  test <@ [ false ] = [ for r in (fromSubject item1 g) do
                          match r with
                          | HasType type2 _ -> yield true
                          | _ -> yield false ] @>

[<Fact>]
let ``Map object``() =
  test <@ [["avalue"]] = [ for r in (fromSubject item3 g) do
                             match r with
                             | Predicate pr3 values ->
                                yield resource.mapO (xsd.string) values
                             | _ -> yield [] ] @>
[<Fact>]
let Traverse =
  test <@ [true] = [ for r in (fromSubject item1 g) do
                         match r with
                         | Predicate pr1 next ->
                           for r' in traverse next do
                           yield true] @>
