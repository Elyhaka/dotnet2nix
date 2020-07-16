{ callPackage, dotnetCorePackages }:

let
  buildDotNetProject = callPackage ./Nix/buildDotNetProject.nix { };
in buildDotNetProject {
    name = "my-project";
    version = "my-version";
    src = ./.;

    target = "linux-x64";
    runtime = dotnetCorePackages.netcore_3_1;
    sdk = dotnetCorePackages.sdk_3_1;
    nugetPkgs = ./dotnet2nix-pkgs.linux-x64.json;
}
