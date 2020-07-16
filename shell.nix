with import <nixpkgs> {};

mkShell {
  DOTNET_CLI_TELEMETRY_OPTOUT = 1;
  buildInputs = [ dotnetCorePackages.sdk_3_1 ];
}
