var target = Argument("target", "Default");

#load foo.cake

Task("Default")
  .Does(() =>
{
  Foo
    .Create()
    .Bar();

  Information(HelloText);
  Information("Building on" + BuildSystem)
});

RunTarget(target);
