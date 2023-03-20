git submodule update --init

rustup target add

cd vcpkg
./bootstrap-vcpkg.bat

./vcpkg install libsrt --triplet x64-windows-static-md
#./vcpkg install openssl --triplet x64-linux
./vcpkg integrate install
cd ..