Target-typed default
--------------------

This feature lets you omit the type parameter in `default(T)`. Simply use `default` when the type `T` can be inferred.

Some examples:
- `string s = default;`
- `int i = default;`
- `void M<T>(List<T> parameter = default) { ... }`
- `var x = flag ? default : parameter;`
- `if (x == default) ...`

### Grammar

```antlr
default_value_expression
    : 'default' '(' type ')'
    | 'default' // new
    ;
```
(in reference to Lucian's [ANTLR grammar](https://github.com/ljw1004/csharpspec/blob/gh-pages/csharp.g4))
