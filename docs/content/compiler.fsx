(*** hide ***)
#I "../../bin/"
(**
Hosted Compiler
===============

This tutorial demonstrates how to host the F# compiler.

> **NOTE:** The API used below is experimental and subject to change. In particular, the 
services in FSharp.Compiler.Service.dll are overlapping and will in the future be made more regular.
This will involve breaking changes to the APIs used for these services.

> **NOTE:** There is a number of options for hosting the F# compiler. The easiest one is to use the 
`fsc.exe` process and pass arguments. 


---------------------------

First, we need to reference the libraries that contain F# interactive service:
*)

#r "FSharp.Compiler.Service.dll"
open Microsoft.FSharp.Compiler.SimpleSourceCodeServices
open System.IO

let scs = SimpleSourceCodeServices()

(**
Now write content to a temporary file:

*)
let fn = Path.GetTempFileName()
let fn2 = Path.ChangeExtension(fn, ".fs")
let fn3 = Path.ChangeExtension(fn, ".dll")

File.WriteAllText(fn2, """
module M

type C() = 
   member x.P = 1

let x = 3 + 4
""")

(**
Now invoke the compiler:
*)

let errors1, exitCode1 = scs.Compile([| "fsc.exe"; "-o"; fn3; "-a"; fn2 |])

(** 

If errors occur you can see this in the 'exitCode' and the returned array of errors:

*)
File.WriteAllText(fn2, """
module M

let x = 1.0 + "" // a type error
""")

let errors1b, exitCode1b = scs.Compile([| "fsc.exe"; "-o"; fn3; "-a"; fn2 |])

(**

Compiling to a dynamic assembly
===============================

You can also compile to a dynamic assembly, which uses the F# Interactive code generator.
This can be useful if you are, for example, in a situation where writing to the file system
is not really an option.

You still have to pass the "-o" option to name the output file, but the output file is not actually written to disk.

The 'None' option indicates that the initiatlization code for the assembly is not executed. 
*)
let errors2, exitCode2, dynAssembly2 = scs.CompileToDynamicAssembly([| "-o"; fn3; "-a"; fn2 |], execute=None)

(*
Passing 'Some' for the 'execute' parameter executes  the initiatlization code for the assembly.
*)
let errors3, exitCode3, dynAssembly3 = scs.CompileToDynamicAssembly([| "-o"; fn3; "-a"; fn2 |],Some(stdout,stderr))
