SCRIPT_DIR="$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"

/bin/bash ${SCRIPT_DIR}/start-postgres.sh
pushd ${SCRIPT_DIR}/../src/Altinn.Notifications/
dotnet watch run 
popd

docker stop notificationsdb