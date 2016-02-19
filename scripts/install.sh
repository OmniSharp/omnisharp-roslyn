if [ -d ~/.omnisharp/local ]; then
  echo "Removing local omnisharp ..."
  rm -rf ~/.omnisharp/local
fi

mkdir -p ~/.omnisharp/local

for framework in dnx451 dnxcore50; do
  ./.dotnet/bin/dotnet publish ./src/Omnisharp -o ~/.omnisharp/local/$framework --framework $framework
done