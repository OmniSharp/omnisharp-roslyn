var target = Argument("target", "Default");

#load foo.cake

Task("Default")
  .Does(() =>
{
  Foo
    .Create()
    .Bar();

  Information("Hello World!");
});

RunTarget(target);
