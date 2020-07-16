with import <nixpkgs> {};

let
  dotnet2nix = callPackage ./default.nix {};
in mkShell {
  DOTNET_CLI_TELEMETRY_OPTOUT = 1;
  buildInputs = [ dotnet2nix ];
}
