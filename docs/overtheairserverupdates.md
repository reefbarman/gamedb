WIP: Server and over the air updates {#serverpage}
==========
(Pro version only)

[TOC]

# Overview {#serveroverview}
The GameDB (Pro) plugin provides functionality for uploading and downloading a gameDB to/from a server. Effectively allowing over the air updates of your data without the need to republish or resubmit your app before getting an update out to users.

Included in the plugin is a server written in Golang that interfaces with AWS and S3/DynamoDB to manage the updates and the uploaded files. This server can be used as is or is provided as an example for writing your own or integrating it with other infrastructure.

The plugin also contains high level helper functions to retrieve the data but also provides APIs for doing most of the separate tasks yourself if custom integration with your app is required.

The data uploaded and downloaded from the server is both compressed and encrypted (more info HERE) and is only a diff between a base version of the data and the current revision meaning it is as small and efficient as possible. The diff format is that defined by the JSON Patch format <http://jsonpatch.com/>.

# Setup {#serversetup}
## Server {#server}
The provided server is easily built and deployed as it comes with build scripts and a docker file. Extra work will be required to integrate it with pre-existing infrastructure but deployment won't be covered here, though I can be contacted to discuss options of providing support to help you deploy the server (This will be charged extra to the price of the plugin)

The server was built using golang version go1.8.3. While earlier or later versions are likely to work just fine, I suggest you use the recommended version to avoid any issues.

Some prerequisite software will need to be installed:

* golang 1.8.3 <https://golang.org/dl/>
* glide <https://github.com/Masterminds/glide>
* docker <https://www.docker.com/>

Within the _Assets/Plugin/GameDBLibary_ folder is a _server.zip_ that you should extract to your go workspace <https://golang.org/doc/install>.

You will then need to in the root of the server directory run the command `glide install` this will download and install all the required dependencies for the server.

After this has been completed you can proceed to build the server by running `./build.sh`. This will compile the executable and build a docker image ready to deploy or test.

Next step is to setup the AWS services you will be interfacing with. This guide won't go into detail on how to set these up but will outline the requirements (If you need extra support feel free to contact me).

***S3***
You will need to either create or use an existing S3 bucket. Files will be uploaded here that will have ACLs set to public-read (If you would rather these are access controlled you will need to modify the server and client to support this).

***DynamoDB***
You will need a DynamoDB table set up that has a _Partition key_ called _tag_ as a string type and a _Sort key_ called _scope_ as a string type. Records regarding the uploaded files will be stored here.

Before testing the server a configuration file needs to be defined see configuration section [below](#server-configuration)

Once you have a _conf.yaml_ that suits your needs then you can start the docker container for testing by running `docker run --v ./conf.yaml:/app/conf.yaml ***TODO GET ACTUAL COMMAND***`

## GameDB Plugin {#gamedbplugin}
To connect the Unity plugin to the server for uploading and managing deployed gameDBs you will need to configure the URLs for your server and for the S3 bucket you previously setup.

To do this open the GameDB Editor by going to ***Window > GameDB > Open Editor*** then in the editor window navigate to the _Deployment_ tab.

There should then be a _Deploy to Server_ section you can expand where you will see two entry fields for _GameDB Server Host_ and _Download Server Host_. Assuming you have a server deployed or running locally you can set these then you should be able to start deploying gameDBs.

Example values for the host fields are:
_GameDB Server Host_: http://localhost:8000 (if running it locally for testing)
_Download Server Host_: https://s3-eu-west-1.amazonaws.com/mybucket

## Server Configuration {#serverconfig}
The server can be configured in a few different ways. These include via a conf.yaml file, or a combination of the conf.yaml and environment variables.

Below is an example conf.yaml file and then an explanation of what each value configures:

    server:
      port: 8000               //The port the server listens on. Recommended to leave as default as the docker container exposes this port
      writeTimeout: 30         //Timeout in seconds for writing a http response
      readTimeout: 30          //Timeout in seconds for reading a http request
      formBufferSize: 10485760 //Max size in bytes for uploaded POST data and parameters

    aws:                       //The AWS config can also be set via environment variables listed here: http://docs.aws.amazon.com/cli/latest/userguide/cli-environment.html if using environment variables for AWS don't set any thing in the conf.yaml (delete the whole AWS section)
      id: ""                   //AWS Credentials ID
      secret: ""               //AWS Credentials Secret
      region: "eu-west-1"      //AWS Region for S3 bucket and DynamoDB table

    gamedb:
      dynamodb:
        tableName: "gamedb"    //Name of previously created DynamoDB table in configured region. Can also be set by environment variable GDB_GAMEDB_DYNAMODB_TABLENAME
      s3:
        bucket: "mybucket"     //Name of previously created S3 bucket in configured region. Can also be set by environment variable GDB_GAMEDB_S3_BUCKET
        storageRoot: "gamedb"  //Top level directory name to store files in, in S3 bucket. Can also be set by environment variable GDB_GAMEDB_S3_STORAGEROOT
      encryption:
        key: "somekey"         //A secure key to use for encrypting uploaded gameDB data to be downloaded by clients.
        salt: "somesalt"       //A secure salt to use for encrypting uploaded gameDB data to be downloaded by clients

    logging:
      level: "info"            //Log level to use can be set based on available log levels here: https://github.com/sirupsen/logrus#level-logging

# Usage {#serverusage}

## Deploying a gameDB {$deployingagamedb}
The GameDB Editor allows you to upload and publish the currently loaded gameDB. This makes it easy to make a change to your data and get it onto a server quickly and efficiently.

***Terminology***
Deployment: A deployment is a set of revision for a particular gameDB scoped by the Scope Name of the uploaded gameDB and by a Tag. A deployment contains information about the current deployed version plus all past/future revisions.

Tag:  A tag is an arbitrary label applied to a particular deployment of a gameDB. Generally this will be something like the client version the gameDB is intended for (ie. 1.1.3) or could be an identifier (ie. open beta). This allows different versions of the same gameDB to be deployed for different needs. If the schema for a gameDB changes so should the tag it is deployed against.

Revision: A revision is a particular incremental change of data for a particular gameDB and tag. Meaning if you make a change to the data without changing schema and you intend to update it for a already deployed gameDB it will be treated as a revision of the server. Which causes it to detect only the differences between the original uploaded gameDB for a particular gameDB/tag and store that only saving bandwidth when the client downloads the new data.

***Retrieving Deployments***
On the _Deployment_ tab of the GameDB Editor, if you expand the _Deploy to Server_ section, you can click the _Retrieve Deployments_ button to query the server for the current list of deployments for the current gameDB. If you have gameDBs already deployed this will update the Tag and Revision drop-downs below.

***Uploading a new revision***
