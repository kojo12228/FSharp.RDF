module Graph

open VDS.RDF
open VDS.RDF.Writing
open VDS.RDF.Parsing

let (++) a b = System.IO.Path.Combine(a, b)

[<CustomEquality; NoComparison>]
type Uri = 
  | Curie of qname : string * segments : string list * ref : string option
  | Sys of System.Uri
  | VDS of IUriNode
  
  override x.ToString() = 
    match x with
    | Curie(q, p, Some r) -> sprintf "%s:%s/%s" q (p |> List.reduce (++)) r
    | Curie(q, p, None) -> sprintf "%s:/%s" q (p |> List.reduce (++))
    | Sys uri -> string uri
    | VDS uri -> string uri.Uri
  
  override u.Equals(u') = 
    match u' with
    | :? Uri as u' -> 
      match u, u' with
      | Sys u, Sys u' -> (string u) = (string u')
      | VDS u, VDS u' -> u = u'
      | Sys u, VDS u' -> (string u) = (string u'.Uri)
      | VDS u, Sys u' -> (string u.Uri) = (string u')
      | Curie _, Curie _ -> (string u) = (string u')
      | _ -> false
    | _ -> false
  
  static member from s = Uri.Sys(System.Uri(s))
  static member from s = Uri.Sys s
  static member toSys (x : Uri) = System.Uri(string x)

[<CustomEquality; NoComparison>]
type Node = 
  | Uri of Uri
  | Blank of IBlankNode
  | Literal of ILiteralNode
  
  static member from (n : INode) = 
    match n with
    | :? IUriNode as n -> Node.Uri(VDS n)
    | :? ILiteralNode as n -> Node.Literal n
    | :? IBlankNode as n -> Node.Blank n
    | _ -> failwith (sprintf "Unknown node %A" (n.GetType()))
  
  static member from c = Node.Uri(Uri.Curie c)
  static member from (u : string) = Node.Uri(Uri.from u)
  static member from (u : System.Uri) = Node.Uri(Uri.from u)
  override n.Equals(n') = 
    match n' with
    | :? Node as n' -> 
      match n, n' with
      | Uri n, Uri n' -> n = n'
      | _ -> false
    | _ -> false

type Subject = 
  | S of Node
  static member from c = S(Node.Uri(Uri.Curie c))
  static member from (u : string) = S(Node.from u)

type Predicate = 
  | P of Node
  static member from c = P(Node.Uri(Uri.Curie c))
  static member from u = P(Node.Uri(Uri.Sys u))
  static member from u = P(Node.Uri(Uri.Sys(System.Uri u)))

type Object = 
  | O of Node
  static member from c = O(Node.Uri(Uri.Curie c))
  static member from u = O(Node.Uri(Uri.Sys u))

type Triple = Subject * Predicate * Object

type Resource = 
  | R of Subject * (Predicate * Object) list

type Graph = 
  | Graph of IGraph
  static member from (s : string) = 
    let g = new VDS.RDF.Graph()
    let p = TurtleParser()
    use sr = new System.IO.StringReader(s)
    p.Load(g, sr)
    Graph g

module triple = 
  let uriNode u (g : IGraph) = g.GetUriNode(u |> Uri.toSys)
  let bySubject u (g : IGraph) = g.GetTriplesWithSubject(uriNode u g)
  let byObject u (g : IGraph) = g.GetTriplesWithObject(uriNode u g)
  let byPredicate u (g : IGraph) = g.GetTriplesWithPredicate(uriNode u g)
  let byPredicateObject p o (g : IGraph) = 
    g.GetTriplesWithPredicateObject(uriNode p g, uriNode o g)
  let bySubjectObject p o (g : IGraph) = 
    g.GetTriplesWithSubjectObject(uriNode p g, uriNode o g)
  let bySubjectPredicate p o (g : IGraph) = 
    g.GetTriplesWithSubjectPredicate(uriNode p g, uriNode o g)
  
  let triplesToStatement (t : VDS.RDF.Triple seq) = 
    t
    |> Seq.groupBy (fun t -> t.Subject)
    |> Seq.map 
         (fun (s, tx) -> 
         R((S(Node.from s)), 
           [ for t in tx -> (P(Node.from t.Predicate), O(Node.from t.Object)) ]))
    |> Seq.toList
  
  let fromSingle (f : Uri -> IGraph -> VDS.RDF.Triple seq) x (Graph g) = 
    f x g |> triplesToStatement
  let fromDouble (f : Uri -> Uri -> IGraph -> VDS.RDF.Triple seq) x y (Graph g) = 
    f x y g |> triplesToStatement

module prefixes = 
  let prov = "http://www.w3.org/ns/prov#"
  let rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#"
  let owl = "http://www.w3.org/2002/07/owl#"
  let cnt = "http://www.w3.org/2011/content#"
  let compilation = "http://nice.org.uk/ns/compilation#"
  let git2prov = "http://nice.org.uk/ns/prov/"

module uri = 
  open prefixes
  
  let a = Uri.from (rdf + "type")

module resource = 
  open triple
  open prefixes
  
  let fromSubject u g = fromSingle bySubject u g
  let fromPredicate u g = fromSingle byPredicate u g
  let fromObject u g = fromSingle byObject u g
  let fromPredicateObject x y g = fromDouble byPredicateObject x y g
  let fromSubjectObject x y g = fromDouble bySubjectObject x y g
  let fromSubjectPredicate x y g = fromDouble bySubjectPredicate x y g
  
  let asTriples (s, px) = 
    seq { 
      for (p, o) in px -> (s, p, o)
    }
  
  //Applies f to the object component of statement
  let mapObject f (O o) = f o
  //Applies f to the object component of all resources
  let mapO f = List.map (mapObject f)
  
  //Traverses object properties for predicate pr
  let mapNext px = 
    [ for p in px do
        match p with
        | (_, O(Node.Uri(Uri.VDS vds))) -> 
          yield! bySubject (Uri.VDS vds) (vds.Graph) |> triplesToStatement
        | _ -> () ]
  
  let (|Is|) (R(S(Uri s), _)) = s
  
  let optionalList = 
    function 
    | x :: xs -> Some(x :: xs)
    | _ -> None
  
  let (|Predicate|_|) pr (R(_, xs)) = 
    xs
    |> Seq.filter (fun ((P p), _) -> p = p)
    |> Seq.map (fun (_, o) -> o)
    |> Seq.toList
    |> optionalList
  
  let (|HasType|_|) t (R(_, xs)) = 
    xs
    |> Seq.filter 
         (fun ((P(Uri p)), (O(Uri o))) -> 
         p = Uri.from (prefixes.rdf + "type") && t = o)
    |> Seq.map (fun (_, (O(Uri t))) -> t)
    |> Seq.toList
    |> optionalList

module xsd = 
  open VDS.RDF
  
  let mapL f n = 
    match n with
    | Node.Literal l -> (f l)
    | _ -> failwith "%A is not a literal node"
  
  let string = mapL (fun l -> l.Value)
  let int = mapL (fun l -> int l.Value)
  let datetime = mapL (fun l -> System.DateTime.Parse l.Value)
  let datetimeoffset = mapL (fun l -> System.DateTimeOffset.Parse l.Value)
