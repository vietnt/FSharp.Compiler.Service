namespace Microsoft.FSharp.Compiler

open Microsoft.FSharp.Compiler
open Microsoft.FSharp.Compiler.Range
open Microsoft.FSharp.Compiler.ErrorLogger
open Microsoft.FSharp.Compiler.AbstractIL.Internal.Library
open Microsoft.FSharp.Compiler.Build


[<RequireQualifiedAccess>]
type (*internal*) Severity = 
    | Warning 
    | Error

type (*internal*) ErrorInfo = 
    { FileName:string
      StartLine:Line0
      EndLine:Line0
      StartColumn:int
      EndColumn:int
      Severity:Severity
      Message:string
      Subcategory:string }
    static member internal CreateFromExceptionAndAdjustEof : PhasedError * bool * bool * range * (Line0*int) -> ErrorInfo
    static member internal CreateFromException : PhasedError * bool * bool * range -> ErrorInfo

// implementation details used by other code in the compiler    
[<Sealed>]
type internal ErrorScope = 
    interface System.IDisposable
    new : unit -> ErrorScope
    member ErrorsAndWarnings : ErrorInfo list
    static member Protect<'a> : range -> (unit->'a) -> (string->'a) -> 'a
    static member ProtectWithDefault<'a> : range -> (unit -> 'a) -> 'a -> 'a
    static member ProtectAndDiscard : range -> (unit -> unit) -> unit

/// Generalized Incremental Builder. This is exposed only for unittesting purposes.
module internal IncrementalBuild =
  // A build scalar.
  type Scalar<'T> = interface end
  /// A build vector.        
  type Vector<'T> = interface end

  /// A set of build rules and the corresponding, possibly partial, results from building.
  type PartialBuild 

  /// Declares a vector build input.
  /// Only required for unit testing.
  val InputScalar : string -> Scalar<'T>

  /// Declares a scalar build input.
  /// Only required for unit testing.
  val InputVector : string -> Vector<'T>

  /// Methods for acting on build Scalars
  /// Only required for unit testing.
  module Scalar = 
      /// Apply a function to one scalar to produce another.
      val Map : string -> ('I -> 'O) -> Scalar<'I> -> Scalar<'O>
      /// Apply a function to scalar value to produce a vector.
      val Multiplex : string -> ('I -> 'O[])->Scalar<'I> -> Vector<'O>

  /// Methods for acting on build Vectors
  /// Only required for unit testing.
  module Vector = 
      /// Maps one vector to another using the given function.    
      val Map : string -> ('I -> 'O) -> Vector<'I> -> Vector<'O>
      /// Updates the creates a new vector with the same items but with 
      /// timestamp specified by the passed-in function.  
      val Stamp : string -> ('I -> System.DateTime) -> Vector<'I> -> Vector<'I>
      /// Apply a function to each element of the vector, threading an accumulator argument
      /// through the computation. Returns intermediate results in a vector.
      val ScanLeft : string -> ('A -> 'I -> Eventually<'A>) -> Scalar<'A> -> Vector<'I> -> Vector<'A>
      /// Apply a function to a vector to get a scalar value.
      val Demultiplex : string -> ('I[] -> 'O)->Vector<'I> -> Scalar<'O>
      /// Convert a Vector into a Scalar.
      val AsScalar: string -> Vector<'I> -> Scalar<'I[]> 

  /// Evaluate a build. Only required for unit testing.
  val Eval : string -> PartialBuild -> PartialBuild
  /// Do one step in the build. Only required for unit testing.
  val Step : (string -> PartialBuild -> PartialBuild option)
  /// Get a scalar vector. Result must be available. Only required for unit testing.
  val GetScalarResult<'T> : string * PartialBuild -> ('T * System.DateTime) option
  /// Get a result vector. All results must be available or thrown an exception. Only required for unit testing.
  val GetVectorResult<'T> : string * PartialBuild -> 'T[]
  /// Get an element of vector result or None if there were no results. Only required for unit testing.
  val GetVectorResultBySlot<'T> : string*int*PartialBuild -> ('T * System.DateTime) option
  
  /// Declare build outputs and bind them to real values.
  /// Only required for unit testing.
  type BuildDescriptionScope = 
       new : unit -> BuildDescriptionScope
       /// Declare a named scalar output.
       member DeclareScalarOutput : name:string * output:Scalar<'T> -> unit
       /// Declare a named vector output.
       member DeclareVectorOutput : name:string * output:Vector<'T> -> unit
       /// Set the conrete inputs for this build. 
       member GetInitialPartialBuild : vectorinputs:(string * int * obj list) list * scalarinputs:(string*obj) list -> PartialBuild

/// Incremental builder for F# parsing and type checking.  
module internal IncrementalFSharpBuild =

    /// Used for unit testing
  type IBEvent =
        | IBEParsed of string // filename
        | IBETypechecked of string // filename
        | IBENuked

    /// Used for unit testing
  val GetMostRecentIncrementalBuildEvents : int -> IBEvent list
    /// Used for unit testing
  val GetCurrentIncrementalBuildEventNum : unit -> int


  type IncrementalBuilder = 
      new : tcConfig : Build.TcConfig * projectDirectory : string * assemblyName : string * niceNameGen : Microsoft.FSharp.Compiler.Ast.NiceNameGenerator *
            lexResourceManager : Microsoft.FSharp.Compiler.Lexhelp.LexResourceManager * sourceFiles : string list * ensureReactive : bool *
            errorLogger : ErrorLogger * keepGeneratedTypedAssembly:bool
        -> IncrementalBuilder

      /// Increment the usage count on the IncrementalBuilder by 1. Ths initial usage count is 0. The returns an IDisposable which will 
      /// decrement the usage count on the entire build by 1 and dispose if it is no longer used by anyone.
      member IncrementUsageCount : unit -> System.IDisposable
     
      /// Check if the builder is not disposed
      member IsAlive : bool

      /// The TcConfig passed in to the builder creation.
      member TcConfig : Build.TcConfig

      /// Raised just before a file is type-checked, to invalidate the state of the file in VS and force VS to request a new direct typecheck of the file.
      /// The incremental builder also typechecks the file (error and intellisense results from the backgroud builder are not
      /// used by VS). 
      member BeforeTypeCheckFile : IEvent<string>

      /// Raised just after the whole project has finished type checking. At this point, accessing the
      /// overall analysis results for the project will be quick.
      member AfterProjectTypeCheck : IEvent<unit>

      /// Raised when a type provider invalidates the build.
      member ImportedCcusInvalidated : IEvent<string>

      /// The list of files the build depends on
      member Dependencies : string list
#if EXTENSIONTYPING
      /// Whether there are any 'live' type providers that may need a refresh when a project is Cleaned
      member ThereAreLiveTypeProviders : bool
#endif
      /// Perform one step in the F# build (type-checking only, the type check is not finalized)
      member Step : unit -> bool

      /// Get the preceding typecheck state of a slot. Return None if the result is not available.
      /// This is a quick operation.
      member GetTypeCheckResultsBeforeFileInProjectIfReady: filename:string -> (Build.TcState * Build.TcImports * Microsoft.FSharp.Compiler.Env.TcGlobals * Build.TcConfig * (PhasedError * bool) list * System.DateTime) option

      /// Get the preceding typecheck state of a slot. Compute the entire type check of the project up
      /// to the necessary point if the result is not available. This may be a long-running operation.
      ///
      // TODO: make this an Eventually (which can be scheduled) or an Async (which can be cancelled)
      member GetTypeCheckResultsBeforeFileInProject : filename:string -> (Build.TcState * Build.TcImports * Microsoft.FSharp.Compiler.Env.TcGlobals * Build.TcConfig * (PhasedError * bool) list * System.DateTime) 

      /// Get the typecheck state after checking a file. Compute the entire type check of the project up
      /// to the necessary point if the result is not available. This may be a long-running operation.
      ///
      // TODO: make this an Eventually (which can be scheduled) or an Async (which can be cancelled)
      member GetTypeCheckResultsAfterFileInProject : filename:string -> (Build.TcState * Build.TcImports * Microsoft.FSharp.Compiler.Env.TcGlobals * Build.TcConfig * (PhasedError * bool) list * System.DateTime) 

      /// Get the typecheck result after the end of the last file. The typecheck of the project is not 'completed'.
      /// This may be a long-running operation.
      ///
      // TODO: make this an Eventually (which can be scheduled) or an Async (which can be cancelled)
      member GetTypeCheckResultsAfterLastFileInProject : unit -> (Build.TcState * Build.TcImports * Microsoft.FSharp.Compiler.Env.TcGlobals * Build.TcConfig * (PhasedError * bool) list * System.DateTime) 

      /// Get the final typecheck result. If 'generateTypedImplFiles' was set on Create then the TypedAssembly will contain implementations.
      /// This may be a long-running operation.
      ///
      // TODO: make this an Eventually (which can be scheduled) or an Async (which can be cancelled)
      member GetTypeCheckResultsForProject : unit -> Build.TcState * TypeChecker.TopAttribs * Tast.TypedAssembly * TypeChecker.TcEnv * Build.TcImports * Env.TcGlobals * Build.TcConfig * (PhasedError * bool) list 

      /// Await the untyped parse results for a particular slot in the vector of parse results.
      ///
      /// This may be a marginally long-running operation (parses are relatively quick, only one file needs to be parsed)
      member GetParseResultsForFile : filename:string -> Ast.ParsedInput option * Range.range * string * (PhasedError * bool) list

      static member CreateBackgroundBuilderForProjectOptions : scriptClosureOptions:LoadClosure option * sourceFiles:string list * commandLineArgs:string list * projectDirectory:string * useScriptResolutionRules:bool * isIncompleteTypeCheckEnvironment : bool -> IncrementalBuilder * ErrorInfo list
