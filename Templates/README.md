# AOTrino templates

    dotnet new install AOTrino.Templates

    dotnet new aotrino          -o MyApp    # a plain HTML page, no build step
    dotnet new aotrino-react    -o MyApp    # React + @aotrino/react
    dotnet new aotrino-fluent   -o MyApp    # Fluent UI + @aotrino/fluent

Then:

    cd MyApp
    dotnet run                              # or dotnet publish -r win-x64 for the single exe

See https://github.com/aelyo-softworks/AOTrino.
