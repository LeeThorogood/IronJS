﻿namespace IronJS.Runtime

open IronJS
open IronJS.Aliases
open IronJS.Tools
open IronJS.Tools.Dlr

open System
open System.Dynamic
open System.Collections.Generic
open System.Runtime.InteropServices

#nowarn "9" //Disables warning about "generation of unverifiable .NET IL code"  

(*=======================================================
  Runtime Environment
  =======================================================*)

[<AllowNullLiteral>]
type Environment (scopeAnalyzer:Ast.Types.Scope -> ClrType -> ClrType list -> Ast.Types.Scope, 
                  exprGenerator:Environment -> ClrType -> ClrType -> Ast.Types.Scope -> Ast.Node -> EtLambda) =
                  
  let mutable classId = 0
  let closureMap = new Dict<ClrType, int>()
  let delegateCache = new Dict<DelegateCell, System.Delegate>()

  [<DefaultValue>] val mutable Globals : Object
  [<DefaultValue>] val mutable UndefinedBox : Box
  [<DefaultValue>] val mutable ObjectClass : Class
  [<DefaultValue>] val mutable FunctionClass : Class
  [<DefaultValue>] val mutable Object_prototype : Object
  [<DefaultValue>] val mutable Function_prototype : Object

  [<DefaultValue>] val mutable AstMap : Dict<int, Ast.Types.Scope * Ast.Node>
  [<DefaultValue>] val mutable GetCrawlers : Dict<int list, GetCrawler>
  [<DefaultValue>] val mutable SetCrawlers : Dict<int list, SetCrawler>

  member x.GetDelegate (func:Function) delegateType types =
    let cell = new DelegateCell(func.AstId, func.ClosureId, delegateType)
    let success, delegate' = delegateCache.TryGetValue(cell)
    if success then delegate'
    else
      let scope, body = x.AstMap.[func.AstId]
      let closureType = func.Closure.GetType()
      let lambdaExpr  = exprGenerator x delegateType closureType (scopeAnalyzer scope closureType types) body
      delegateCache.[cell] <- lambdaExpr.Compile()
      delegateCache.[cell]
  
  member x.GetClosureId clrType = 
    let success, id = closureMap.TryGetValue clrType
    if success 
      then id
      else closureMap.[clrType] <- closureMap.Count
           closureMap.Count - 1

  member x.NextClassId = 
    classId <- classId + 1
    classId

  static member Create sa eg =
    let env = new Environment(sa, eg)
    //Maps
    env.AstMap <- new Dict<int, Ast.Types.Scope * Ast.Node>()
    env.GetCrawlers <- new Dict<int list, GetCrawler>()
    env.SetCrawlers <- new Dict<int list, SetCrawler>()

    //Base classes
    env.ObjectClass   <- new Class(env.NextClassId, new Dict<string, int>())
    env.FunctionClass <- env.ObjectClass.GetSubClass("length", env.NextClassId)

    //Object.prototype
    env.Object_prototype    <- new Object(env.ObjectClass, null, 32)
    env.Function_prototype  <- new Object(env.ObjectClass, env.Object_prototype, 32)
    env.Object_prototype.SetDouble("foo", 2.0, env)

    //Globals
    env.Globals <- new Object(env.ObjectClass, env.Object_prototype, 128)

    //Init undefined box
    env.UndefinedBox.Type <- Types.Undefined
    env.UndefinedBox.Clr  <- Undefined.Instance

    env

(*=======================================================
  Dynamic Box 
  =======================================================*)

and [<StructLayout(LayoutKind.Explicit)>] Box =
  struct
    [<FieldOffset(0)>] val mutable Clr    : obj 

    #if FAST_CAST
    [<FieldOffset(0)>] val mutable Object : Object
    [<FieldOffset(0)>] val mutable Func   : Function
    [<FieldOffset(0)>] val mutable String : string
    #endif

    #if X64
    [<FieldOffset(8)>]  val mutable Bool   : bool
    [<FieldOffset(8)>]  val mutable Int    : int32
    [<FieldOffset(8)>]  val mutable Double : double
    [<FieldOffset(16)>] val mutable Type   : Types
    #else // X86
    [<FieldOffset(4)>]  val mutable Bool   : bool
    [<FieldOffset(4)>]  val mutable Int    : int32
    [<FieldOffset(4)>]  val mutable Double : double
    [<FieldOffset(12)>] val mutable Type   : Types
    #endif
  end
    
(*=======================================================
  Object + Support objects
  =======================================================*)

    (*==== Class representing an objects hidden class ====*)
and [<AllowNullLiteral>] Class =
  val mutable ClassId : int
  val mutable SubClasses : Dict<string, Class>
  val mutable Variables : Dict<string, int>

  new(classId, variables) = {
    ClassId = classId
    Variables = variables
    SubClasses = new Dict<string, Class>();
  }

  member x.GetSubClass (name:string, newId:int) =
    //Note: I hate interfacing with C# code
    let success, cls = x.SubClasses.TryGetValue name
    if success then cls
    else
      let variables = new Dict<string, int>(x.Variables)
      variables.Add(name, variables.Count)
      x.SubClasses.Add(name, new Class(newId, variables)) 
      x.SubClasses.[name]

  member x.GetIndex varName =
    x.Variables.TryGetValue(varName)

    (*==== A plain javascript object ====*)
and [<AllowNullLiteral>] Object =
  val mutable ClassId : int
  val mutable Class : Class
  val mutable Properties : Box array
  val mutable Prototype : Object

  new(cls, prototype, initSize) = {
    Class = cls
    ClassId = cls.ClassId
    Properties = Array.zeroCreate<Box> initSize
    Prototype = prototype
  }

  member x.SetDouble (name:string, value:double, env:Environment) =
    let mutable box = new Box()
    box.Double <- value
    box.Type <- Types.Double
    x.Set(new SetCache(name), ref box, env)

  member x.Set (cache:SetCache, value:Box byref, env:Environment) =
    x.Update (cache, ref value)
    if cache.ClassId <> x.ClassId then
      x.Create (cache, ref value, env)

  member x.Update (cache:SetCache, value:Box byref) =
    let success, index = x.Class.GetIndex cache.Name
    if success then 
      cache.ClassId <- x.ClassId
      cache.Index   <- index
      x.Properties.[index] <- value

  member x.Create (cache:SetCache, value:Box byref, env:Environment) =
    x.Class   <- x.Class.GetSubClass(cache.Name, env.NextClassId)
    x.ClassId <- x.Class.ClassId

    if x.Class.Variables.Count > x.Properties.Length then
      let newProperties = Array.zeroCreate<Box> (x.Properties.Length * 2)
      System.Array.Copy(x.Properties, newProperties, x.Properties.Length)
      x.Properties <- newProperties

    x.Update(cache, ref value)

  member x.Get (cache:GetCache, env:Environment) =
    let success, index = x.Class.GetIndex cache.Name
    if success && x.Properties.[index].Type <> Types.Nothing then
      cache.ClassId <- x.ClassId
      cache.Index   <- index
      x.Properties.[index]
    else
      cache.ClassId <- -1
      cache.Index   <- -1
      env.UndefinedBox

  member x.Has name =
    let success, index = x.Class.GetIndex name
    if success && x.Properties.[index].Type <> Types.Nothing
      then index
      else -1
      
  member x.PrototypeHas name =
    let mutable index = -1
    let mutable classIds = []
    let mutable prototype = x.Prototype

    while index = -1 && prototype <> null do
      index       <- prototype.Has name
      classIds    <- prototype.ClassId :: classIds
      prototype   <- prototype.Prototype

    index, classIds

  interface System.Dynamic.IDynamicMetaObjectProvider with
    member self.GetMetaObject expr = new ObjectMeta(expr, self) :> MetaObj
    
    (*==== Object meta class for DLR bindings ====*)
and ObjectMeta(expr, jsObj:Object) =
  inherit System.Dynamic.DynamicMetaObject(expr, Dlr.Restrict.notAtAll, jsObj)

  override x.BindConvert(binder) =
    if binder.Type = typedefof<Object> then
      let expr = Dlr.Expr.castT<Object> x.Expression
      let restrict = Dlr.Restrict.byType x.Expression x.LimitType
      new MetaObj(expr, restrict)
    else
      failwith "ObjectMeta.BindConvert not implemented for other types then Runtime.Core.Object"

(*=======================================================
  Function + Support objects
  =======================================================*)
  
    (*==== Scope class, representing a functions scope during runtime ====*)
and [<AllowNullLiteral>] Scope = 
  val mutable Objects : Object ResizeArray
  val mutable EvalObject : Object
  val mutable ScopeLevel : int

  new(objects, evalObject, scopeLevel) = {
    Objects = objects
    EvalObject = evalObject
    ScopeLevel = scopeLevel
  } 

    (*==== Closure environment base class ====*)
and Closure =
  val mutable Scopes : Scope ResizeArray

  new(scopes) = {
    Scopes = scopes
  }

    (*==== Class representing a javascript function ====*)
and [<AllowNullLiteral>] Function =
  inherit Object

  val mutable Closure : Closure
  val mutable AstId : int
  val mutable ClosureId : int
  val mutable Environment : Environment

  new(astId, closureId, closure, env:Environment) = { 
    inherit Object(env.FunctionClass, null, 2)
    AstId = astId
    ClosureId = closureId
    Closure = closure
    Environment = env
  }

  member x.Compile<'a when 'a :> Delegate and 'a : null> (types:ClrType list) =
     (x.Environment.GetDelegate x typeof<'a> types) :?> 'a

(*=======================================================
  Inline Caches
  =======================================================*)

    (*==== Custom Delegates for Set/Get inline caches ====*)
and GetCrawler =
  delegate of GetCache * Object * Environment -> Box

and SetCrawler =
  delegate of SetCache * Object * Box byref * Environment -> unit
  
    (*==== Inline cache for property get operations ====*)
and GetCache(name) as x =

  [<DefaultValue>] val mutable Name : string
  [<DefaultValue>] val mutable ClassId : int
  [<DefaultValue>] val mutable Index : int
  [<DefaultValue>] val mutable Crawler : GetCrawler
  [<DefaultValue>] val mutable ThrowOnMissing : bool

  do x.Name <- name
  do x.ClassId <- -1
  do x.Index <- -1
  do x.Crawler <- null
  do x.ThrowOnMissing <- false
  
  static member New(name:string) =
    let cache = Dlr.Expr.constant (new GetCache(name))
    cache, 
    Expr.field cache "ClassId", 
    Expr.field cache "Index", 
    Expr.field cache "Crawler"
    
    (*==== Inline cache for property set operations ====*)
and SetCache =

  val mutable Name : string
  val mutable ClassId : int
  val mutable Index : int
  val mutable Crawler : SetCrawler

  new(name) = {
    Name = name
    ClassId = -1
    Index = -1
    Crawler = null
  }

  static member New(name:string) =
    let cache = Dlr.Expr.constant (new SetCache(name))
    cache, 
    Expr.field cache "ClassId", 
    Expr.field cache "Index", 
    Expr.field cache "Crawler"
    
    (*==== Inline cache for object creation ====*)
and NewCache =
  val mutable Class : Class
  val mutable ClassId : int
  val mutable InitSize : int
  val mutable LastCreated : Object

  new(class') = {
    Class = class'
    ClassId = class'.ClassId
    InitSize = 1
    LastCreated = null
  }

  static member New(class') =
    new NewCache(class')
    
    (*==== Inline cache for function invocation ====*)
and InvokeCache<'a> when 'a :> Delegate and 'a : null =
  val mutable AstId : int
  val mutable ClosureId : int
  val mutable Delegate : 'a
  val mutable ArgTypes : ClrType list

  new(argTypes) = {
    AstId = -1
    ClosureId = -1
    Delegate = null
    ArgTypes = argTypes
  }

  member x.Update (fnc:Function) =
    x.Delegate <- fnc.Compile<'a>(x.ArgTypes) 

module private Cache = 

  let classIdEq expr n =
    Expr.eq (Expr.field expr "ClassId") (Expr.constant n)
  
  let crawlPrototypeChain expr n = 
    Seq.fold (fun s _ -> Expr.field expr "Prototype") expr (seq{0..n-1})

  //Object + All Prototypes must not
  //be null and have matching ClassIds
  let buildCondition object' classIds = 
    (Expr.andChain
      (List.mapi 
        (fun i x -> 
          let prototype = crawlPrototypeChain object' i
          (Expr.and' (Expr.notDefault prototype) (classIdEq prototype x))
        )
        (classIds)
      )
    )

(*==== GetCache implementations ====*)
type GetCache with

  //This function handles the updating of
  //the cache cell in case of a miss
  member x.Update (obj:Object, env:Environment) =
    let box = obj.Get(x, env)

    //We found what we were looking for
    if x.ClassId = obj.ClassId then
      box

    //If not...
    else
      //Check if a prototype has it
      let index, classIds = obj.PrototypeHas x.Name

      //The constants -4 and -2 here could be anything
      //They are used to differ types of configurations
      //from eachother in the lambdaCache SafeDict key
      let throwToggle = if x.ThrowOnMissing then -4 else -2
      let wasFoundToggle = if index < 0 then -4 else -2

      //Build key and try to find an already cached crawler
      let cacheKey = throwToggle :: wasFoundToggle :: obj.ClassId :: classIds
      let success, cached = env.GetCrawlers.TryGetValue cacheKey

      let crawler = 
        //If we found a cached crawler use it
        if success then cached 

        //Else build a new one
        else
          //Parameters
          let cache = Expr.paramT<GetCache> "~cache"
          let object' = Expr.paramT<Object> "~object"
          let env' = Expr.paramT<Environment> "~env"

          //Body differs
          //depending on if...
          let body = 
            if index >= 0 then
              //... we found the property
              (Expr.access 
                (Expr.field (Cache.crawlPrototypeChain object' (classIds.Length)) "Properties") 
                [Expr.field cache "index"]
              )
            else
              //... or not
              (Expr.field env' "UndefinedBox")

          //Build lambda expression
          let lambda = 
            (Expr.lambdaT<GetCrawler> 
              [cache; object'; env']
              (Expr.ternary 
                (Cache.buildCondition object' (obj.ClassId :: classIds))
                //If condition holds, execute body
                (body)
                //If condition fails, update
                (Expr.call cache "Update" [object'; env'])
              )
            )

          //Compile and to add it to cache
          env.GetCrawlers.[cacheKey] <- lambda.Compile()
          env.GetCrawlers.[cacheKey]

      //Setup cache to be ready for next hit
      x.Index   <- index //Save index so we know which offset to look at
      x.ClassId <- -1 //This makes sure we will hit the crawler next time
      
      x.Crawler <- crawler //Save crawler
      x.Crawler.Invoke(x, obj, env) //Use crawler to get result

(*==== SetCache implementations ====*)
type SetCache with

  member x.Update (obj:Object, value:Box byref, env:Environment) =
    //First try to do a normal update
    obj.Update(x, ref value)

    //And if we don't succeed
    if x.ClassId <> obj.ClassId then
      
      //Check if a prototype has it
      let index, classIds = obj.PrototypeHas x.Name

      //If we didn't find it in a Prototype
      //means we should create it on our current object
      if index < 0 then 
        obj.Create(x, ref value, env)

      //If we actually did find it, we need to
      //create a crawler that can set the property
      //for us in the future
      else
        //Build key and try to find an already cached crawler
        let cacheKey = obj.ClassId :: classIds
        let success, cached = env.SetCrawlers.TryGetValue cacheKey

        let crawler =
          if success then cached
          else
            //Parameters
            let cache = Expr.paramT<SetCache> "~cache"
            let object' = Expr.paramT<Object> "~object"
            let value = Expr.param "~value" (typeof<Box>.MakeByRefType()) 
            let env' = Expr.paramT<Environment> "~env"

            //Body differs
            //depending on if...
            let body = 
              (Expr.assign
                (Expr.access 
                  (Expr.field (Cache.crawlPrototypeChain object' (classIds.Length)) "Properties") 
                  [Expr.field cache "index"]
                )
                (value)
              )

            //Build lambda expression
            let lambda = 
              (Expr.lambdaT<SetCrawler> 
                [cache; object'; value; env']
                (Expr.ternary 
                  (Cache.buildCondition object' (obj.ClassId :: classIds))
                  //If condition holds, execute body
                  (Expr.block [body; Expr.void'])
                  //If condition fails, update
                  (Expr.call cache "Update" [object'; value; env'])
                )
              )

            //Compile and to add it to cache
            env.SetCrawlers.[cacheKey] <- lambda.Compile()
            env.SetCrawlers.[cacheKey]

        //Setup cache to be ready for next hit
        x.Index   <- index //Same as GetCache
        x.ClassId <- -1 //Same as GetCache
      
        x.Crawler <- crawler //Same as GetCache
        x.Crawler.Invoke(x, obj, ref value, env) //Same as GetCache
