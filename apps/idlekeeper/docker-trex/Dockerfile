# The official install talks about Fedora packages, so let's see
# if Centos is close enough...
FROM nvidia/cuda:11.4.2-runtime-ubuntu20.04
ENV DEBIAN_FRONTEND=noninteractive
# We will need Git to pull the repo
RUN apt update && apt -y install wget && rm -rf /var/lib/apt/lists/*

# The packages mentioned in the INSTALL phase, except:
# jansson-devel - We're going to build our own since the version 2.4.x doesn't have json_array_foreach
# make - Added it because Centos doesn't have it even after getting autoconf/automake
#RUN yum -y -q install gcc gcc-c++ make wget autoconf automake install openssl-devel libcurl-devel zlib-devel

# Create a user to do the build
ENV BUILD_FOLDER=/minerbuild
ENV APP_FOLDER=/app
ENV APP_USER=minerbuilder
ENV TREX_VERSION=0.24.8
ENV CUDA_VERSION=10.0

RUN adduser $APP_USER && \
    mkdir $BUILD_FOLDER && \
    chown $APP_USER.users $BUILD_FOLDER

# Clone from the git repo
RUN cd $BUILD_FOLDER && \
    wget https://github.com/trexminer/T-Rex/releases/download/"$TREX_VERSION"/t-rex-"$TREX_VERSION"-linux.tar.gz && \
    tar xzvf t-rex-"$TREX_VERSION"-linux.tar.gz

ENV TREX_FOLDER=$BUILD_FOLDER

# Copy the binary to the /app folder
RUN mkdir $APP_FOLDER && \
    chown $APP_USER.users $APP_FOLDER && \
    cp $TREX_FOLDER/t-rex $APP_FOLDER

# Redefine the app user and folder - note app folder must be the same as the first stage
ENV APP_FOLDER=/app

# Get the non-devel versions of the libraries that we need
RUN apt update && apt -y install openssl libcurl4 zlib1g libgomp1-i386-cross libgomp1-amd64-cross &&  \
    rm -rf /var/lib/apt/lists/*

# Load the Jansson library that's now built
RUN echo /usr/local/lib > /etc/ld.so.conf.d/userlocal.conf && \
    ldconfig

# Symlink the app to /usr/local/bin
RUN ln -s $APP_FOLDER/t-rex /usr/local/bin/t-rex && \
    chown -R root.root $APP_FOLDER

# Recreate and switch to the app user for this build

RUN mv $APP_FOLDER/t-rex $APP_FOLDER/binary
RUN touch $APP_FOLDER/t-rex
RUN echo "ldconfig && ./binary" > $APP_FOLDER/t-rex
RUN chmod +x $APP_FOLDER/t-rex

CMD [ t-rex ]
