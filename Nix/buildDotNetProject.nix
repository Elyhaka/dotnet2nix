{ stdenv
, stdenvNoCC
, lib
, symlinkJoin
, fetchurl
, unzip
, icu
, openssl
}:

{ name ? "${args'.pname}-${args'.version}"
, version
, buildInputs ? []
, nativeBuildInputs ? []
, passthru ? {}
, patches ? []
, meta ? {}

, target
, runtime
, sdk
, nugetPkgs

, ...}@args':

with builtins;

let
  args = removeAttrs args' [ "sdk" "nugetPkgs" ];

  rpath = lib.makeLibraryPath (buildInputs ++ [
    stdenv.cc.cc
    icu
    openssl
  ]);
  dynamicLinker = stdenv.cc.bintools.dynamicLinker;

  nugetPackage = let
    parsedLockFile = builtins.fromJSON (builtins.readFile nugetPkgs);

    fetchNuGet = { name, version, metadata }:
      let
        nupkgName = lib.strings.toLower "${name}.${version}.nupkg";
      in stdenvNoCC.mkDerivation {
        name = "${name}-${version}";

        src = fetchurl {
          url = metadata.resolvedUrl;
          name = "${name}.${version}.zip";
          sha256 = metadata.hash;
        };

        sourceRoot = ".";

        buildInputs = [ unzip ];

        dontStrip = true;
        dontPatch = true;

        unpackPhase = ''
          unzip -qq -o $src
        '';

        installPhase = ''
          mkdir -p $out
          chmod +r *.nuspec
          cp *.nuspec $out
          cp $src $out/${nupkgName}
        '';
      };
  in symlinkJoin {
    name = "${name}-nuget-pkgs";
    paths = map fetchNuGet parsedLockFile;
  };

  package = stdenv.mkDerivation (args // {
    nativeBuildInputs = nativeBuildInputs ++ [ sdk ];

    buildPhase = args.buildPhase or ''
      export DOTNET_CLI_TELEMETRY_OPTOUT=1
      export HOME="$(mktemp -d)"
      dotnet publish --nologo \
        -r ${target} --self-contained \
        -c Release -o out \
        --source ${nugetPackage}
    '';

    installPhase = args.installPhase or ''
      runHook preInstall
      mkdir -p $out/bin
      cp -r ./out/* $out
      ln -s $out/${name} $out/bin/${name}
      runHook postInstall
    '';

    dontPatchELF = args.dontPatchELF or true;
    postFixup = args.postFixup or ''
      patchelf --set-interpreter "${dynamicLinker}" \
        --set-rpath '$ORIGIN:${rpath}' $out/${name}

      find $out -type f -name "*.so" -exec \
        patchelf --set-rpath '$ORIGIN:${rpath}' {} ';'    
    '';
  });
in package
