using System;
using System.Linq;

namespace IronJS.Runtime {
  using ArgLink = Tuple<ParameterStorageType, Int32>;

  /// <summary>
  /// A <see cref="CommonObject"/> used as an argument to a <see cref="FunctionObject"/>.
  /// </summary>
  public class ArgumentsObject : CommonObject {
    private Boolean linkIntact = true;


    /// <summary>
    /// Initializes a new instance of the <see cref="ArgumentsObject"/> class.
    /// </summary>
    /// <param name="env">The environment.</param>
    /// <param name="linkMap">The link map.</param>
    /// <param name="privateScope">The private scope.</param>
    /// <param name="sharedScope">The shared scope.</param>
    public ArgumentsObject(Environment env,
                           ArgLink[] linkMap,
                           BoxedValue[] privateScope,
                           BoxedValue[] sharedScope)
      : base(env, env.Maps.Base, env.Prototypes.Object) {

      PrivateScope = privateScope;
      SharedScope = sharedScope;
      LinkMap = linkMap;
    }


    /// <summary>
    /// Gets or sets a value indicating whether to keep the link intact.
    /// </summary>
    /// <value>
    ///   <c>true</c> to keep the link intact; otherwise, <c>false</c>.
    /// </value>
    public Boolean LinkIntact {
      get => linkIntact;
      set => linkIntact = value;
    }

    /// <summary>
    /// Gets or sets the link map.
    /// </summary>
    /// <value>
    /// The link map.
    /// </value>
    public ArgLink[] LinkMap { get; set; }

    /// <summary>
    /// Gets or sets whether the <see cref="ArgumentsObject"/> should be limited
    /// to the private scope or not.
    /// </summary>
    /// <value>
    /// 	<c>true</c> if the <see cref="ArgumentsObject"/> should be limited
    /// to the private scope; otherwise <c>false</c>.
    /// </value>
    public BoxedValue[] PrivateScope { get; set; }

    /// <summary>
    /// Gets or sets the shared scope.
    /// </summary>
    /// <value>
    /// The shared scope.
    /// </value>
    public BoxedValue[] SharedScope { get; set; }


    /// <summary>
    /// Creates a <see cref="ArgumentsObject"/> for the specified <see cref="FunctionObject"/>
    /// <paramref name="funcObj"/>.
    /// </summary>
    /// <param name="funcObj">The function for which to create an <see cref="ArgumentsObject"/>.</param>
    /// <param name="privateScope">The private scope.</param>
    /// <param name="sharedScope">The shared scope.</param>
    /// <param name="namedArgsPassed">The number of named arguments that is passed.</param>
    /// <param name="extraArgs">The extra arguments.</param>
    /// <returns>
    /// A <see cref="ArgumentsObject"/> for the specified <see cref="FunctionObject"/>
    /// <paramref name="funcObj"/>.
    /// </returns>
    public static ArgumentsObject CreateForFunction(FunctionObject funcObj,
                                                    BoxedValue[] privateScope,
                                                    BoxedValue[] sharedScope,
                                                    Int32 namedArgsPassed,
                                                    BoxedValue[] extraArgs) {
      // TODO: This method has no tests. [asbjornu]

      Int32 length = namedArgsPassed + extraArgs.Length;
      ArgLink[] storage = funcObj.MetaData.ParameterStorage.Take(namedArgsPassed).ToArray();

      ArgumentsObject argObj = new ArgumentsObject(funcObj.Env, storage, privateScope, sharedScope);

      argObj.CopyLinkedValues();
      argObj.Put("constructor", funcObj.Env.Constructors.Object);
      argObj.Put("length", length, DescriptorAttrs.DontEnum);
      argObj.Put("callee", funcObj, DescriptorAttrs.DontEnum);

      for (var i = 0; i < extraArgs.Length; ++i)
        argObj.Put((uint)(i + namedArgsPassed), extraArgs[i]);

      return argObj;
    }


    /// <summary>
    /// Creates a <see cref="ArgumentsObject"/> for the specified variadic
    /// <see cref="FunctionObject"/> <paramref name="funcObj"/>.
    /// </summary>
    /// <param name="funcObj">The f.</param>
    /// <param name="privateScope">The private scope.</param>
    /// <param name="sharedScope">The shared scope.</param>
    /// <param name="variadicArgs">The variadic args.</param>
    /// <returns>
    /// A <see cref="ArgumentsObject"/> for the specified variadic <see cref="FunctionObject"/>
    /// <paramref name="funcObj"/>.
    /// </returns>
    public static ArgumentsObject CreateForVariadicFunction(FunctionObject funcObj,
                                                            BoxedValue[] privateScope,
                                                            BoxedValue[] sharedScope,
                                                            BoxedValue[] variadicArgs) {
      // TODO: This method has no tests. [asbjornu]

      ArgumentsObject argObj = new ArgumentsObject(funcObj.Env,
                                                   funcObj.MetaData.ParameterStorage,
                                                   privateScope,
                                                   sharedScope
                                                  );

      argObj.CopyLinkedValues();
      argObj.Put("constructor", funcObj.Env.Constructors.Object, 0xffffff08);
      argObj.Put("length", variadicArgs.Length, 2);
      argObj.Put("callee", funcObj, 2);

      // TODO: R# says this expression will always evaluate to false. Rewrite or remove? [asbjornu]
      if (variadicArgs is Object) {
        Int32 i = funcObj.MetaData.ParameterStorage.Length;
        for (; i < variadicArgs.Length; i++)
          argObj.Put((UInt32)i, variadicArgs[i]);
      }

      return argObj;
    }


    /// <summary>
    /// Deletes the property at the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The index of the property to delete.</param>
    /// <returns>
    ///   <c>true</c> if the deletion succeeded; otherwise <c>false</c>.
    /// </returns>
    public override Boolean Delete(UInt32 index) {
      Int32 ii = (Int32)index;

      if (LinkIntact && ii < LinkMap.Length) {
        CopyLinkedValues();
        LinkIntact = false;
        PrivateScope = null;
        SharedScope = null;
      }

      return base.Delete(index);
    }


    /// <summary>
    /// Gets the property at the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The index of the property to get.</param>
    /// <returns>
    /// The property at the specified <paramref name="index"/>.
    /// </returns>
    public override BoxedValue Get(UInt32 index) {
      Int32 ii = (Int32)index;

      if (LinkIntact && ii < LinkMap.Length) {
        ArgLink link = LinkMap[ii];
        switch (link.Item1) {
          case ParameterStorageType.Private:
            return PrivateScope[link.Item2];

          case ParameterStorageType.Shared:
            return SharedScope[link.Item2];
        }
      }

      return base.Get(index);
    }


    /// <summary>
    /// Determines whether the <see cref="ArgumentsObject"/> has a
    /// property at the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>
    ///   <c>true</c> if the <see cref="ArgumentsObject"/> has a
    /// property at the specified <paramref name="index"/>; otherwise, <c>false</c>.
    /// </returns>
    public override Boolean Has(UInt32 index)
      => (LinkIntact && (Int32)index < LinkMap.Length) || base.Has(index);


    /// <summary>
    /// Puts the <paramref name="value"/> at the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="value">The value.</param>
    public override void Put(UInt32 index, BoxedValue value) {
      Int32 ii = (Int32)index;

      if (LinkIntact && ii < LinkMap.Length) {
        ArgLink link = LinkMap[ii];
        switch (link.Item1) {
          case ParameterStorageType.Private:
            PrivateScope[link.Item2] = value;
            break;

          case ParameterStorageType.Shared:
            SharedScope[link.Item2] = value;
            break;
        }
      }

      base.Put(index, value);
    }


    /// <summary>
    /// Puts the <paramref name="value"/> at the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="value">The value.</param>
    public override void Put(UInt32 index, Double value) => Put(index, BoxedValue.Box(value));


    /// <summary>
    /// Puts the <paramref name="value"/> at the specified <paramref name="index"/>
    /// with the provided <paramref name="tag"/>.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="value">The value.</param>
    /// <param name="tag">The tag.</param>
    public override void Put(UInt32 index, Object value, UInt32 tag)
      => Put(index, BoxedValue.Box(value, tag));


    /// <summary>
    /// Copies the linked values.
    /// </summary>
    public void CopyLinkedValues() {
      // TODO: Can this method be made private? [asbjornu]
      for (Int32 i = 0; i < LinkMap.Length; ++i) {
        ArgLink link = LinkMap[i];
        switch (link.Item1) {
          case ParameterStorageType.Private:
            base.Put((UInt32)i, PrivateScope[link.Item2]);
            break;

          case ParameterStorageType.Shared:
            base.Put((UInt32)i, SharedScope[link.Item2]);
            break;
        }
      }
    }
  }
}
